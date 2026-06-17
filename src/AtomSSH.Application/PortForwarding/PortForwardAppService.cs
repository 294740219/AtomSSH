using AtomSSH.Application.Common;
using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Application.PortForwarding;

public sealed class PortForwardAppService
{
    private readonly IPortForwardProfileRepository _forwards;
    private readonly ISshProfileRepository _profiles;
    private readonly IConnectionRoutePlanner _routePlanner;
    private readonly IPortForwardRuntime _runtime;

    public PortForwardAppService(
        IPortForwardProfileRepository forwards,
        ISshProfileRepository profiles,
        IConnectionRoutePlanner routePlanner,
        IPortForwardRuntime runtime)
    {
        _forwards = forwards;
        _profiles = profiles;
        _routePlanner = routePlanner;
        _runtime = runtime;
    }

    public Task<OperationResult<IReadOnlyList<PortForwardProfile>>> ListAsync(CancellationToken cancellationToken)
    {
        return _forwards.ListAsync(cancellationToken);
    }

    public Task<OperationResult> SaveAsync(PortForwardProfile profile, CancellationToken cancellationToken)
    {
        return _forwards.SaveAsync(profile, cancellationToken);
    }

    public async Task<OperationResult<PortForwardInstanceId>> StartAsync(
        PortForwardProfile forwardProfile,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(forwardProfile.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult<PortForwardInstanceId>.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", forwardProfile.ProfileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(profile.Value, cancellationToken).ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult<PortForwardInstanceId>.Failure(route.Error!);
        }

        return await _runtime.StartAsync(forwardProfile, route.Value, cancellationToken).ConfigureAwait(false);
    }

    public Task<OperationResult> StopAsync(PortForwardInstanceId instanceId, CancellationToken cancellationToken)
    {
        return _runtime.StopAsync(instanceId, cancellationToken);
    }
}
