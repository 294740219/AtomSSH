using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Network.Routes;

public class ConnectionRoutePlanner : IConnectionRoutePlanner
{
    private readonly ISshProfileRepository? _profiles;
    private readonly INetworkInventoryStore? _inventory;

    public ConnectionRoutePlanner(ISshProfileRepository? profiles = null, INetworkInventoryStore? inventory = null)
    {
        _profiles = profiles;
        _inventory = inventory;
    }

    public async Task<OperationResult<ConnectionRoute>> PlanAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        if (profile.JumpHostProfileId is not null)
        {
            return await CreateJumpHostRouteAsync(
                profile,
                profile.JumpHostProfileId.Value,
                cancellationToken).ConfigureAwait(false);
        }

        var inventoryRoute = await TryPlanFromInventoryAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!inventoryRoute.Succeeded || inventoryRoute.Value is not null)
        {
            return inventoryRoute.Succeeded
                ? OperationResult<ConnectionRoute>.Success(inventoryRoute.Value!)
                : OperationResult<ConnectionRoute>.Failure(inventoryRoute.Error!);
        }

        var route = new ConnectionRoute(
            ConnectionRouteKind.Direct,
            profile.Endpoint,
            Array.Empty<JumpHostRoute>());

        return OperationResult<ConnectionRoute>.Success(route);
    }

    private async Task<OperationResult<ConnectionRoute?>> TryPlanFromInventoryAsync(
        SshProfile profile,
        CancellationToken cancellationToken)
    {
        if (_inventory is null)
        {
            return OperationResult<ConnectionRoute?>.Success(null);
        }

        var spaces = await _inventory.ListSpacesAsync(cancellationToken).ConfigureAwait(false);
        if (!spaces.Succeeded)
        {
            return OperationResult<ConnectionRoute?>.Failure(spaces.Error!);
        }

        foreach (var space in spaces.Value!)
        {
            var nodes = await _inventory.ListNodesAsync(space.Id, cancellationToken).ConfigureAwait(false);
            if (!nodes.Succeeded)
            {
                return OperationResult<ConnectionRoute?>.Failure(nodes.Error!);
            }

            var targetNode = nodes.Value!.FirstOrDefault(node => node.ProfileId == profile.Id);
            if (targetNode is null || targetNode.Role == NetworkNodeRole.JumpHost)
            {
                continue;
            }

            var jumpNode = nodes.Value!.FirstOrDefault(node =>
                node.Role == NetworkNodeRole.JumpHost
                && node.ProfileId is not null
                && node.ProfileId != profile.Id);
            if (jumpNode?.ProfileId is null)
            {
                return OperationResult<ConnectionRoute?>.Success(null);
            }

            var route = await CreateJumpHostRouteAsync(profile, jumpNode.ProfileId.Value, cancellationToken)
                .ConfigureAwait(false);
            return route.Succeeded
                ? OperationResult<ConnectionRoute?>.Success(route.Value)
                : OperationResult<ConnectionRoute?>.Failure(route.Error!);
        }

        return OperationResult<ConnectionRoute?>.Success(null);
    }

    private async Task<OperationResult<ConnectionRoute>> CreateJumpHostRouteAsync(
        SshProfile profile,
        SshProfileId jumpHostProfileId,
        CancellationToken cancellationToken)
    {
        if (_profiles is null)
        {
            return OperationResult<ConnectionRoute>.Failure(new SshError(
                SshErrorKind.Configuration,
                "Jump host route planning requires an SSH profile repository."));
        }

        var jumpHost = await _profiles.GetAsync(jumpHostProfileId, cancellationToken)
            .ConfigureAwait(false);
        if (!jumpHost.Succeeded || jumpHost.Value is null)
        {
            return OperationResult<ConnectionRoute>.Failure(jumpHost.Error ?? new SshError(
                SshErrorKind.Validation,
                "Jump host profile was not found.",
                jumpHostProfileId.Value.ToString()));
        }

        var jumpRoute = new ConnectionRoute(
            ConnectionRouteKind.JumpHost,
            profile.Endpoint,
            [new JumpHostRoute(jumpHost.Value.Id, jumpHost.Value.Endpoint)]);

        return OperationResult<ConnectionRoute>.Success(jumpRoute);
    }
}

public sealed class FakeConnectionRoutePlanner : ConnectionRoutePlanner
{
    public FakeConnectionRoutePlanner(ISshProfileRepository? profiles = null)
        : base(profiles)
    {
    }
}
