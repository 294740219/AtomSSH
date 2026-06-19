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
    private static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(30);

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
            ConnectionRouteKind.JumpHost or ConnectionRouteKind.ProxyJumpChain => await ConnectViaJumpChainAsync(
                profile,
                route,
                clientFactory,
                cancellationToken).ConfigureAwait(false),
            _ => OperationResult<SshNetClientConnection<TClient>>.Failure(new SshError(
                SshErrorKind.Validation,
                "Unsupported connection route kind.",
                $"Route kind: {route.Kind}"))
        };
    }

    private async Task<OperationResult<SshNetClientConnection<TClient>>> ConnectViaJumpChainAsync<TClient>(
        SshProfile targetProfile,
        ConnectionRoute route,
        Func<ConnectionInfo, TClient> clientFactory,
        CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        if (route.JumpHosts.Count == 0)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(new SshError(
                SshErrorKind.Validation,
                "Jump host route requires at least one jump host.",
                $"Jump host count: {route.JumpHosts.Count}"));
        }

        var disposables = new List<IDisposable>();
        SshNetClientConnection<SshClient>? currentJumpConnection = null;
        var ownershipTransferred = false;
        try
        {
            foreach (var jumpHostRoute in route.JumpHosts)
            {
                var jumpProfile = await _profiles.GetAsync(jumpHostRoute.ProfileId, cancellationToken).ConfigureAwait(false);
                if (!jumpProfile.Succeeded || jumpProfile.Value is null)
                {
                    return OperationResult<SshNetClientConnection<TClient>>.Failure(jumpProfile.Error ?? new SshError(
                        SshErrorKind.Validation,
                        "Jump host profile was not found.",
                        jumpHostRoute.ProfileId.Value.ToString()));
                }

                OperationResult<SshNetClientConnection<SshClient>> jumpClientResult;
                if (currentJumpConnection is null)
                {
                    jumpClientResult = await ConnectDirectAsync(
                        jumpProfile.Value,
                        jumpHostRoute.Endpoint,
                        jumpHostRoute.Endpoint,
                        info => new SshClient(info),
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var nextJumpPort = CreateStartedForward(currentJumpConnection.Client, jumpHostRoute.Endpoint);
                    disposables.Add(nextJumpPort);
                    var nextJumpLoopbackEndpoint = new SshEndpoint(
                        LocalForwardEndpoint.Host,
                        checked((int)nextJumpPort.BoundPort));
                    jumpClientResult = await ConnectDirectAsync(
                        jumpProfile.Value,
                        nextJumpLoopbackEndpoint,
                        jumpHostRoute.Endpoint,
                        info => new SshClient(info),
                        cancellationToken).ConfigureAwait(false);
                }

                if (!jumpClientResult.Succeeded || jumpClientResult.Value is null)
                {
                    return OperationResult<SshNetClientConnection<TClient>>.Failure(jumpClientResult.Error!);
                }

                currentJumpConnection = jumpClientResult.Value;
                disposables.Add(currentJumpConnection);
            }

            if (currentJumpConnection is null)
            {
                return OperationResult<SshNetClientConnection<TClient>>.Failure(new SshError(
                    SshErrorKind.Internal,
                    "Jump host chain did not produce a connection."));
            }

            var targetForward = CreateStartedForward(currentJumpConnection.Client, route.Target);
            disposables.Add(targetForward);
            var loopbackEndpoint = new SshEndpoint(
                LocalForwardEndpoint.Host,
                checked((int)targetForward.BoundPort));
            var targetResult = await ConnectDirectAsync(
                targetProfile,
                loopbackEndpoint,
                route.Target,
                clientFactory,
                cancellationToken).ConfigureAwait(false);
            if (!targetResult.Succeeded || targetResult.Value is null)
            {
                return OperationResult<SshNetClientConnection<TClient>>.Failure(targetResult.Error!);
            }

            ownershipTransferred = true;
            return OperationResult<SshNetClientConnection<TClient>>.Success(new SshNetClientConnection<TClient>(
                targetResult.Value.Client,
                disposables));
        }
        catch (Exception exception)
        {
            return OperationResult<SshNetClientConnection<TClient>>.Failure(SshNetErrorMapper.Map(exception));
        }
        finally
        {
            if (!ownershipTransferred)
            {
                DisposeAll(disposables);
            }
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

        connectionInfoResult.Value.Timeout = DefaultConnectionTimeout;

        TClient? client = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prepareHostKey = await _hostKeyVerifier.PrepareAsync(hostKeyEndpoint, cancellationToken)
                .ConfigureAwait(false);
            if (!prepareHostKey.Succeeded)
            {
                return OperationResult<SshNetClientConnection<TClient>>.Failure(prepareHostKey.Error!);
            }

            client = clientFactory(connectionInfoResult.Value);
            client.KeepAliveInterval = DefaultKeepAliveInterval;
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

    private static ForwardedPortLocal CreateStartedForward(SshClient client, SshEndpoint target)
    {
        var forwardedPort = new ForwardedPortLocal(
            LocalForwardEndpoint.Host.Value,
            (uint)LocalForwardEndpoint.Port,
            target.Host.Value,
            (uint)target.Port);
        client.AddForwardedPort(forwardedPort);
        forwardedPort.Start();
        return forwardedPort;
    }

    private static void DisposeAll(IEnumerable<IDisposable> disposables)
    {
        foreach (var disposable in disposables.Reverse())
        {
            disposable.Dispose();
        }
    }
}
