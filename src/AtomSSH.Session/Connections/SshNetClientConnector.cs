using AtomSSH.Core.Credentials;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Session.Errors;
using AtomSSH.Session.HostKeys;
using Renci.SshNet;

namespace AtomSSH.Session.Connections;

internal sealed class SshNetClientConnector
{
    private static readonly SshEndpoint LocalForwardEndpoint = new(new("127.0.0.1"), 0);

    private readonly ISshProfileRepository _profiles;
    private readonly ICredentialResolver _credentialResolver;
    private readonly ISshNetConnectionInfoFactory _connectionInfoFactory;
    private readonly SshNetHostKeyVerifier _hostKeyVerifier;

    public SshNetClientConnector(
        ISshProfileRepository profiles,
        ICredentialResolver credentialResolver,
        ISshNetConnectionInfoFactory connectionInfoFactory,
        SshNetHostKeyVerifier hostKeyVerifier)
    {
        _profiles = profiles;
        _credentialResolver = credentialResolver;
        _connectionInfoFactory = connectionInfoFactory;
        _hostKeyVerifier = hostKeyVerifier;
    }

    public Task<OperationResult<SshNetClientConnection<SshClient>>> ConnectSshClientAsync(
        SshProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        return ConnectAsync(profile, route, endpoint => new SshClient(endpoint), cancellationToken);
    }

    public Task<OperationResult<SshNetClientConnection<SftpClient>>> ConnectSftpClientAsync(
        SshProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        return ConnectAsync(profile, route, endpoint => new SftpClient(endpoint), cancellationToken);
    }

    private async Task<OperationResult<SshNetClientConnection<TClient>>> ConnectAsync<TClient>(
        SshProfile profile,
        ConnectionRoute route,
        Func<ConnectionInfo, TClient> clientFactory,
        CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        return route.Kind switch
        {
            ConnectionRouteKind.Direct => await ConnectDirectAsync(
                profile,
                route.Target,
                route.Target,
                clientFactory,
                cancellationToken).ConfigureAwait(false),
            ConnectionRouteKind.JumpHost => await ConnectViaJumpHostAsync(
                profile,
                route,
                clientFactory,
                cancellationToken).ConfigureAwait(false),
            _ => OperationResult<SshNetClientConnection<TClient>>.Failure(new SshError(
                SshErrorKind.Validation,
                "ProxyJump chain routes are not implemented in the SSH.NET connector yet.",
                $"Route kind: {route.Kind}"))
        };
    }

    private async Task<OperationResult<SshNetClientConnection<TClient>>> ConnectViaJumpHostAsync<TClient>(
        SshProfile targetProfile,
        ConnectionRoute route,
        Func<ConnectionInfo, TClient> clientFactory,
        CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        if (route.JumpHosts.Count != 1)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(new SshError(
                SshErrorKind.Validation,
                "Exactly one jump host is supported by the first jump host connector.",
                $"Jump host count: {route.JumpHosts.Count}"));
        }

        var jumpHostRoute = route.JumpHosts[0];
        var jumpProfile = await _profiles.GetAsync(jumpHostRoute.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!jumpProfile.Succeeded || jumpProfile.Value is null)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(jumpProfile.Error ?? new SshError(
                SshErrorKind.Validation,
                "Jump host profile was not found.",
                jumpHostRoute.ProfileId.Value.ToString()));
        }

        var jumpClientResult = await ConnectDirectAsync(
            jumpProfile.Value,
            jumpHostRoute.Endpoint,
            jumpHostRoute.Endpoint,
            info => new SshClient(info),
            cancellationToken).ConfigureAwait(false);
        if (!jumpClientResult.Succeeded || jumpClientResult.Value is null)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(jumpClientResult.Error!);
        }

        var jumpConnection = jumpClientResult.Value;
        ForwardedPortLocal? forwardedPort = null;
        try
        {
            forwardedPort = new ForwardedPortLocal(
                LocalForwardEndpoint.Host.Value,
                (uint)LocalForwardEndpoint.Port,
                route.Target.Host.Value,
                (uint)route.Target.Port);
            jumpConnection.Client.AddForwardedPort(forwardedPort);
            forwardedPort.Start();

            var loopbackEndpoint = new SshEndpoint(
                LocalForwardEndpoint.Host,
                checked((int)forwardedPort.BoundPort));
            var targetResult = await ConnectDirectAsync(
                targetProfile,
                loopbackEndpoint,
                route.Target,
                clientFactory,
                cancellationToken).ConfigureAwait(false);
            if (!targetResult.Succeeded || targetResult.Value is null)
            {
                forwardedPort.Dispose();
                jumpConnection.Dispose();
                return OperationResult<SshNetClientConnection<TClient>>.Failure(targetResult.Error!);
            }

            return OperationResult<SshNetClientConnection<TClient>>.Success(new SshNetClientConnection<TClient>(
                targetResult.Value.Client,
                [jumpConnection, forwardedPort]));
        }
        catch (Exception exception)
        {
            forwardedPort?.Dispose();
            jumpConnection.Dispose();
            return OperationResult<SshNetClientConnection<TClient>>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    private async Task<OperationResult<SshNetClientConnection<TClient>>> ConnectDirectAsync<TClient>(
        SshProfile profile,
        SshEndpoint connectionEndpoint,
        SshEndpoint hostKeyEndpoint,
        Func<ConnectionInfo, TClient> clientFactory,
        CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        if (profile.CredentialRef is null)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile does not reference a credential."));
        }

        var credentialResult = await _credentialResolver.ResolveAsync(profile.CredentialRef.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!credentialResult.Succeeded || credentialResult.Value is null)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(credentialResult.Error!);
        }

        await using var credentialLease = credentialResult.Value;
        var connectionInfoResult = _connectionInfoFactory.Create(profile, connectionEndpoint, credentialLease);
        if (!connectionInfoResult.Succeeded || connectionInfoResult.Value is null)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(connectionInfoResult.Error!);
        }

        TClient? client = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            client = clientFactory(connectionInfoResult.Value);
            client.HostKeyReceived += (_, args) => _hostKeyVerifier.Verify(hostKeyEndpoint, args);
            client.Connect();

            return OperationResult<SshNetClientConnection<TClient>>.Success(new SshNetClientConnection<TClient>(client));
        }
        catch (HostKeyRejectedException exception)
        {
            client?.Dispose();
            return OperationResult<SshNetClientConnection<TClient>>.Failure(exception.Error);
        }
        catch (Exception exception)
        {
            client?.Dispose();
            return OperationResult<SshNetClientConnection<TClient>>.Failure(SshNetErrorMapper.Map(exception));
        }
    }
}
