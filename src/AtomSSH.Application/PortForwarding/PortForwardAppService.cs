using AtomSSH.Application.Common;
using AtomSSH.Core.Network;
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

    public async Task<OperationResult> SaveAsync(PortForwardProfile profile, CancellationToken cancellationToken)
    {
        var validation = ValidateProfile(profile);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var sshProfile = await _profiles.GetAsync(profile.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!sshProfile.Succeeded)
        {
            return OperationResult.Failure(sshProfile.Error!);
        }

        if (sshProfile.Value is null)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Port forwarding profile references an SSH profile that does not exist.",
                profile.ProfileId.Value.ToString()));
        }

        return await _forwards.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var forwards = await _forwards.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!forwards.Succeeded)
        {
            return OperationResult.Failure(forwards.Error!);
        }

        if (forwards.Value!.All(profile => profile.Id != id))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Port forwarding profile was not found.",
                id.ToString()));
        }

        var statuses = await _runtime.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!statuses.Succeeded)
        {
            return OperationResult.Failure(statuses.Error!);
        }

        if (statuses.Value!.Any(status =>
            status.PortForwardProfileId == id
            && status.State is PortForwardState.Starting or PortForwardState.Running))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Port forwarding profile has active runtime instances.",
                id.ToString()));
        }

        return await _forwards.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult<PortForwardInstanceId>> StartAsync(
        PortForwardProfile forwardProfile,
        CancellationToken cancellationToken)
    {
        var validation = ValidateProfile(forwardProfile);
        if (!validation.Succeeded)
        {
            return OperationResult<PortForwardInstanceId>.Failure(validation.Error!);
        }

        var profile = await _profiles.GetAsync(forwardProfile.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult<PortForwardInstanceId>.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", forwardProfile.ProfileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
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

    public Task<OperationResult<IReadOnlyList<PortForwardStatus>>> ListRuntimeStatusAsync(CancellationToken cancellationToken)
    {
        return _runtime.ListAsync(cancellationToken);
    }

    private static OperationResult ValidateProfile(PortForwardProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Port forwarding profile name is required."));
        }

        var local = ValidateEndpoint(profile.Local, "Local");
        if (!local.Succeeded)
        {
            return local;
        }

        if (profile.Kind is PortForwardKind.Local or PortForwardKind.Remote && profile.Remote is null)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                $"{profile.Kind} port forwarding requires a remote endpoint."));
        }

        if (profile.Remote is not null)
        {
            var remote = ValidateEndpoint(profile.Remote, "Remote");
            if (!remote.Succeeded)
            {
                return remote;
            }
        }

        return OperationResult.Success();
    }

    private static OperationResult ValidateEndpoint(PortForwardEndpoint endpoint, string label)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Host))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                $"{label} port forwarding host is required."));
        }

        if (endpoint.Port <= 0 || endpoint.Port > 65535)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                $"{label} port forwarding port must be between 1 and 65535.",
                endpoint.Port.ToString()));
        }

        return OperationResult.Success();
    }
}
