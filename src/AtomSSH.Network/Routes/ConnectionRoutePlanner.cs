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

    public async Task<OperationResult<ConnectionRoute>> PlanAsync(
        ConnectionRoutePlanningRequest request,
        CancellationToken cancellationToken)
    {
        var profile = request.TargetProfile;
        if (request.PreferredJumpHostProfileId is not null)
        {
            return await CreateJumpHostRouteAsync(
                profile,
                request.PreferredJumpHostProfileId.Value,
                cancellationToken).ConfigureAwait(false);
        }

        if (profile.JumpHostProfileId is not null)
        {
            return await CreateJumpHostRouteAsync(
                profile,
                profile.JumpHostProfileId.Value,
                cancellationToken).ConfigureAwait(false);
        }

        var inventoryRoute = request.AllowInventoryFallback
            ? await TryPlanFromInventoryAsync(request, cancellationToken).ConfigureAwait(false)
            : OperationResult<ConnectionRoute?>.Success(null);
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
        ConnectionRoutePlanningRequest request,
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

        var candidateSpaces = request.NetworkSpaceId is null
            ? spaces.Value!
            : spaces.Value!.Where(space => space.Id == request.NetworkSpaceId.Value).ToArray();
        var matchedRoutes = new List<ConnectionRoute>();

        foreach (var space in candidateSpaces)
        {
            var nodes = await _inventory.ListNodesAsync(space.Id, cancellationToken).ConfigureAwait(false);
            if (!nodes.Succeeded)
            {
                return OperationResult<ConnectionRoute?>.Failure(nodes.Error!);
            }

            var targetNode = nodes.Value!.FirstOrDefault(node => node.ProfileId == request.TargetProfile.Id);
            if (targetNode is null || targetNode.Role == NetworkNodeRole.JumpHost)
            {
                continue;
            }

            var jumpNode = nodes.Value!.FirstOrDefault(node =>
                node.Role == NetworkNodeRole.JumpHost
                && node.ProfileId is not null
                && node.ProfileId != request.TargetProfile.Id);
            if (jumpNode?.ProfileId is null)
            {
                continue;
            }

            var route = await CreateJumpHostRouteAsync(request.TargetProfile, jumpNode.ProfileId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (!route.Succeeded)
            {
                return OperationResult<ConnectionRoute?>.Failure(route.Error!);
            }

            matchedRoutes.Add(route.Value!);
        }

        return matchedRoutes.Count switch
        {
            0 => OperationResult<ConnectionRoute?>.Success(null),
            1 => OperationResult<ConnectionRoute?>.Success(matchedRoutes[0]),
            _ => OperationResult<ConnectionRoute?>.Failure(new SshError(
                SshErrorKind.Validation,
                "Connection route planning is ambiguous.",
                "Multiple network spaces contain the target profile. Specify a network space or jump host."))
        };
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
