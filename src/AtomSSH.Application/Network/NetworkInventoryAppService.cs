using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;

namespace AtomSSH.Application.Network;

public sealed class NetworkInventoryAppService
{
    private readonly INetworkInventoryStore _inventory;
    private readonly IConnectionRoutePlanner _routePlanner;
    private readonly INetworkDiagnosticsService _diagnostics;

    public NetworkInventoryAppService(
        INetworkInventoryStore inventory,
        IConnectionRoutePlanner routePlanner,
        INetworkDiagnosticsService diagnostics)
    {
        _inventory = inventory;
        _routePlanner = routePlanner;
        _diagnostics = diagnostics;
    }

    public Task<OperationResult<IReadOnlyList<NetworkSpace>>> ListSpacesAsync(CancellationToken cancellationToken)
    {
        return _inventory.ListSpacesAsync(cancellationToken);
    }

    public Task<OperationResult<IReadOnlyList<NetworkNode>>> ListNodesAsync(Guid networkSpaceId, CancellationToken cancellationToken)
    {
        return _inventory.ListNodesAsync(networkSpaceId, cancellationToken);
    }

    public Task<OperationResult> SaveSpaceAsync(NetworkSpace space, CancellationToken cancellationToken)
    {
        return _inventory.SaveSpaceAsync(space, cancellationToken);
    }

    public Task<OperationResult> SaveNodeAsync(NetworkNode node, CancellationToken cancellationToken)
    {
        return _inventory.SaveNodeAsync(node, cancellationToken);
    }

    public Task<OperationResult<ConnectionRoute>> PlanRouteAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        return _routePlanner.PlanAsync(profile, cancellationToken);
    }

    public Task<OperationResult<NetworkDiagnosticResult>> DiagnoseAsync(ConnectionRoute route, CancellationToken cancellationToken)
    {
        return _diagnostics.DiagnoseAsync(route, cancellationToken);
    }
}
