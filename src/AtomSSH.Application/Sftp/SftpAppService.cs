using AtomSSH.Application.Common;
using AtomSSH.Core.Network;
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
        var pathValidation = ValidateRemotePath(path);
        if (!pathValidation.Succeeded)
        {
            return OperationResult<IReadOnlyList<SftpItem>>.Failure(pathValidation.Error!);
        }

        var profile = await _profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult<IReadOnlyList<SftpItem>>.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", profileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
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
        var pathValidation = ValidateRemotePath(path);
        if (!pathValidation.Succeeded)
        {
            return pathValidation;
        }

        var profile = await _profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", profileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult.Failure(route.Error!);
        }

        return await _browser.DeleteAsync(profile.Value, route.Value, path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> CreateDirectoryAsync(
        SshProfileId profileId,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        var pathValidation = ValidateRemotePath(path);
        if (!pathValidation.Succeeded)
        {
            return pathValidation;
        }

        var profile = await _profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", profileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult.Failure(route.Error!);
        }

        return await _browser.CreateDirectoryAsync(profile.Value, route.Value, path, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> RenameAsync(
        SshProfileId profileId,
        RemotePath sourcePath,
        RemotePath targetPath,
        CancellationToken cancellationToken)
    {
        var sourceValidation = ValidateRemotePath(sourcePath);
        if (!sourceValidation.Succeeded)
        {
            return sourceValidation;
        }

        var targetValidation = ValidateRemotePath(targetPath);
        if (!targetValidation.Succeeded)
        {
            return targetValidation;
        }

        var profile = await _profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", profileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult.Failure(route.Error!);
        }

        return await _browser.RenameAsync(profile.Value, route.Value, sourcePath, targetPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private static OperationResult ValidateRemotePath(RemotePath path)
    {
        return string.IsNullOrWhiteSpace(path.Value)
            ? OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Remote path is required."))
            : OperationResult.Success();
    }
}
