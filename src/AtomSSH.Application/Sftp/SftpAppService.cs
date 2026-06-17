using AtomSSH.Application.Common;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Sftp;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Application.Sftp;

public sealed class SftpAppService
{
    private readonly ISshProfileRepository _profiles;
    private readonly IConnectionRoutePlanner _routePlanner;
    private readonly ISftpBrowser _browser;

    public SftpAppService(
        ISshProfileRepository profiles,
        IConnectionRoutePlanner routePlanner,
        ISftpBrowser browser)
    {
        _profiles = profiles;
        _routePlanner = routePlanner;
        _browser = browser;
    }

    public async Task<OperationResult<IReadOnlyList<SftpItem>>> ListAsync(
        SshProfileId profileId,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult<IReadOnlyList<SftpItem>>.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", profileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(profile.Value, cancellationToken).ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult<IReadOnlyList<SftpItem>>.Failure(route.Error!);
        }

        return await _browser.ListAsync(profile.Value, route.Value, path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteAsync(
        SshProfileId profileId,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", profileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(profile.Value, cancellationToken).ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult.Failure(route.Error!);
        }

        return await _browser.DeleteAsync(profile.Value, route.Value, path, cancellationToken).ConfigureAwait(false);
    }
}
