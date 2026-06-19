using System.Collections.Concurrent;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Session.Connections;
using AtomSSH.Session.Errors;
using Renci.SshNet;

namespace AtomSSH.Session.PortForwarding;

internal sealed class SshNetPortForwardRuntime : IPortForwardRuntime
{
    private readonly ISshProfileRepository _profiles;
    private readonly SshNetClientConnector _connector;
    private readonly ConcurrentDictionary<PortForwardInstanceId, RuntimePortForwardInstance> _instances = new();

    public SshNetPortForwardRuntime(
        ISshProfileRepository profiles,
        SshNetClientConnector connector)
    {
        _profiles = profiles;
        _connector = connector;
    }

    public async Task<OperationResult<PortForwardInstanceId>> StartAsync(
        PortForwardProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        var sshProfileResult = await _profiles.GetAsync(profile.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!sshProfileResult.Succeeded || sshProfileResult.Value is null)
        {
            return OperationResult<PortForwardInstanceId>.Failure(sshProfileResult.Error ?? new SshError(
                SshErrorKind.Validation,
                "SSH profile was not found.",
                profile.ProfileId.Value.ToString()));
        }

        var connectionResult = await _connector.ConnectSshClientAsync(sshProfileResult.Value, route, cancellationToken)
            .ConfigureAwait(false);
        if (!connectionResult.Succeeded || connectionResult.Value is null)
        {
            return OperationResult<PortForwardInstanceId>.Failure(connectionResult.Error!);
        }

        var connection = connectionResult.Value;
        try
        {
            var forwardedPortResult = CreateForwardedPort(profile);
            if (!forwardedPortResult.Succeeded || forwardedPortResult.Value is null)
            {
                connection.Dispose();
                return OperationResult<PortForwardInstanceId>.Failure(forwardedPortResult.Error!);
            }

            var forwardedPort = forwardedPortResult.Value;
            connection.Client.AddForwardedPort(forwardedPort);
            forwardedPort.Start();

            var instanceId = PortForwardInstanceId.New();
            if (!_instances.TryAdd(instanceId, new RuntimePortForwardInstance(profile.Id, profile.ProfileId, connection, forwardedPort)))
            {
                forwardedPort.Dispose();
                connection.Dispose();
                return OperationResult<PortForwardInstanceId>.Failure(new SshError(
                    SshErrorKind.Internal,
                    "Port forward instance could not be registered."));
            }

            return OperationResult<PortForwardInstanceId>.Success(instanceId);
        }
        catch (Exception exception)
        {
            connection.Dispose();
            return OperationResult<PortForwardInstanceId>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    public Task<OperationResult> StopAsync(PortForwardInstanceId instanceId, CancellationToken cancellationToken)
    {
        if (!_instances.TryRemove(instanceId, out var state))
        {
            return Task.FromResult(OperationResult.Success());
        }

        try
        {
            state.Dispose();
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception exception)
        {
            return Task.FromResult(OperationResult.Failure(SshNetErrorMapper.Map(exception)));
        }
    }

    public Task<OperationResult<IReadOnlyList<PortForwardStatus>>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PortForwardStatus> statuses = _instances
            .Select(pair => pair.Value.ToStatus(pair.Key))
            .ToArray();
        return Task.FromResult(OperationResult<IReadOnlyList<PortForwardStatus>>.Success(statuses));
    }

    private static OperationResult<ForwardedPort> CreateForwardedPort(PortForwardProfile profile)
    {
        try
        {
            return profile.Kind switch
            {
                PortForwardKind.Local => CreateLocalForward(profile),
                PortForwardKind.Remote => CreateRemoteForward(profile),
                PortForwardKind.DynamicSocks => OperationResult<ForwardedPort>.Success(
                    new ForwardedPortDynamic(profile.Local.Host, (uint)profile.Local.Port)),
                _ => OperationResult<ForwardedPort>.Failure(new SshError(
                    SshErrorKind.Validation,
                    "Unsupported port forward kind."))
            };
        }
        catch (Exception exception)
        {
            return OperationResult<ForwardedPort>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    private static OperationResult<ForwardedPort> CreateLocalForward(PortForwardProfile profile)
    {
        if (profile.Remote is null)
        {
            return OperationResult<ForwardedPort>.Failure(new SshError(
                SshErrorKind.Validation,
                "Local port forwarding requires a remote endpoint."));
        }

        return OperationResult<ForwardedPort>.Success(new ForwardedPortLocal(
            profile.Local.Host,
            (uint)profile.Local.Port,
            profile.Remote.Host,
            (uint)profile.Remote.Port));
    }

    private static OperationResult<ForwardedPort> CreateRemoteForward(PortForwardProfile profile)
    {
        if (profile.Remote is null)
        {
            return OperationResult<ForwardedPort>.Failure(new SshError(
                SshErrorKind.Validation,
                "Remote port forwarding requires a remote bind endpoint."));
        }

        return OperationResult<ForwardedPort>.Success(new ForwardedPortRemote(
            profile.Remote.Host,
            (uint)profile.Remote.Port,
            profile.Local.Host,
            (uint)profile.Local.Port));
    }

    private sealed class RuntimePortForwardInstance : IDisposable
    {
        public RuntimePortForwardInstance(
            Guid portForwardProfileId,
            SshProfileId profileId,
            SshNetClientConnection<SshClient> connection,
            ForwardedPort forwardedPort)
        {
            PortForwardProfileId = portForwardProfileId;
            ProfileId = profileId;
            Connection = connection;
            ForwardedPort = forwardedPort;
        }

        private Guid PortForwardProfileId { get; }

        private SshProfileId ProfileId { get; }

        private SshNetClientConnection<SshClient> Connection { get; }

        private ForwardedPort ForwardedPort { get; }

        public PortForwardStatus ToStatus(PortForwardInstanceId instanceId)
        {
            var state = ForwardedPort.IsStarted
                ? PortForwardState.Running
                : PortForwardState.Stopped;

            return new PortForwardStatus(instanceId, PortForwardProfileId, ProfileId, state);
        }

        public void Dispose()
        {
            if (ForwardedPort.IsStarted)
            {
                ForwardedPort.Stop();
            }

            ForwardedPort.Dispose();

            Connection.Dispose();
        }
    }
}
