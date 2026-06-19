using AtomSSH.Application.Common;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Application.Sessions;

public sealed class SessionAppService
{
    private readonly ISshProfileRepository _profiles;
    private readonly IConnectionRoutePlanner _routePlanner;
    private readonly ISshSessionRuntime _sessionRuntime;

    public SessionAppService(
        ISshProfileRepository profiles,
        IConnectionRoutePlanner routePlanner,
        ISshSessionRuntime sessionRuntime)
    {
        _profiles = profiles;
        _routePlanner = routePlanner;
        _sessionRuntime = sessionRuntime;
    }

    public async Task<OperationResult<SshSessionInstanceId>> OpenTerminalAsync(
        SshProfileId profileId,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult<SshSessionInstanceId>.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", profileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult<SshSessionInstanceId>.Failure(route.Error!);
        }

        return await _sessionRuntime.OpenTerminalAsync(profile.Value, route.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<OperationResult> CloseAsync(SshSessionInstanceId sessionId, CancellationToken cancellationToken)
    {
        return _sessionRuntime.CloseAsync(sessionId, cancellationToken);
    }

    public Task<OperationResult<SshSessionSnapshot>> GetSnapshotAsync(
        SshSessionInstanceId sessionId,
        CancellationToken cancellationToken)
    {
        return _sessionRuntime.GetSnapshotAsync(sessionId, cancellationToken);
    }

    public Task<OperationResult<IReadOnlyList<SshSessionSnapshot>>> ListSnapshotsAsync(CancellationToken cancellationToken)
    {
        return _sessionRuntime.ListSnapshotsAsync(cancellationToken);
    }
}
