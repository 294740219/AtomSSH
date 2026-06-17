using System.Collections.Concurrent;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Session.PortForwarding;

public sealed class FakePortForwardRuntime : IPortForwardRuntime
{
    private readonly ConcurrentDictionary<PortForwardInstanceId, PortForwardProfile> _instances = new();

    public Task<OperationResult<PortForwardInstanceId>> StartAsync(
        PortForwardProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        var instanceId = PortForwardInstanceId.New();
        _instances[instanceId] = profile;
        return Task.FromResult(OperationResult<PortForwardInstanceId>.Success(instanceId));
    }

    public Task<OperationResult> StopAsync(PortForwardInstanceId instanceId, CancellationToken cancellationToken)
    {
        _instances.TryRemove(instanceId, out _);
        return Task.FromResult(OperationResult.Success());
    }
}
