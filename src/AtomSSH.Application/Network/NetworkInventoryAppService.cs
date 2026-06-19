using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

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
        if (space.Id == Guid.Empty)
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network space id cannot be empty.")));
        }

        if (string.IsNullOrWhiteSpace(space.Name))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network space name is required.")));
        }

        return _inventory.SaveSpaceAsync(space, cancellationToken);
    }

    public async Task<OperationResult> DeleteSpaceAsync(Guid networkSpaceId, CancellationToken cancellationToken)
    {
        if (networkSpaceId == Guid.Empty)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network space id cannot be empty."));
        }

        var spaces = await _inventory.ListSpacesAsync(cancellationToken).ConfigureAwait(false);
        if (!spaces.Succeeded)
        {
            return OperationResult.Failure(spaces.Error!);
        }

        if (spaces.Value!.All(space => space.Id != networkSpaceId))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network space was not found.",
                networkSpaceId.ToString()));
        }

        var nodes = await _inventory.ListNodesAsync(networkSpaceId, cancellationToken).ConfigureAwait(false);
        if (!nodes.Succeeded)
        {
            return OperationResult.Failure(nodes.Error!);
        }

        if (nodes.Value!.Count > 0)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network space contains nodes and cannot be deleted.",
                networkSpaceId.ToString()));
        }

        return await _inventory.DeleteSpaceAsync(networkSpaceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SaveNodeAsync(NetworkNode node, CancellationToken cancellationToken)
    {
        if (node.NetworkSpaceId == Guid.Empty)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network node must reference a network space."));
        }

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network node name is required."));
        }

        if (string.IsNullOrWhiteSpace(node.Address.Host))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network node host is required."));
        }

        if (node.Address.Port <= 0 || node.Address.Port > 65535)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network node port must be between 1 and 65535.",
                node.Address.Port.ToString()));
        }

        var spaces = await _inventory.ListSpacesAsync(cancellationToken).ConfigureAwait(false);
        if (!spaces.Succeeded)
        {
            return OperationResult.Failure(spaces.Error!);
        }

        if (spaces.Value!.All(space => space.Id != node.NetworkSpaceId))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Network node references a network space that does not exist.",
                node.NetworkSpaceId.ToString()));
        }

        return await _inventory.SaveNodeAsync(node, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteNodeAsync(NetworkNodeId nodeId, CancellationToken cancellationToken)
    {
        var spaces = await _inventory.ListSpacesAsync(cancellationToken).ConfigureAwait(false);
        if (!spaces.Succeeded)
        {
            return OperationResult.Failure(spaces.Error!);
        }

        foreach (var space in spaces.Value!)
        {
            var nodes = await _inventory.ListNodesAsync(space.Id, cancellationToken).ConfigureAwait(false);
            if (!nodes.Succeeded)
            {
                return OperationResult.Failure(nodes.Error!);
            }

            if (nodes.Value!.Any(node => node.Id == nodeId))
            {
                return await _inventory.DeleteNodeAsync(nodeId, cancellationToken).ConfigureAwait(false);
            }
        }

        return OperationResult.Failure(new SshError(
            SshErrorKind.Validation,
            "Network node was not found.",
            nodeId.Value.ToString()));
    }

    public Task<OperationResult<ConnectionRoute>> PlanRouteAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        return _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile), cancellationToken);
    }

    public Task<OperationResult<NetworkDiagnosticResult>> DiagnoseAsync(ConnectionRoute route, CancellationToken cancellationToken)
    {
        return _diagnostics.DiagnoseAsync(route, cancellationToken);
    }

}
