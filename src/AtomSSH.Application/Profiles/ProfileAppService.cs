using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Application.Profiles;

public sealed class ProfileAppService
{
    private readonly ISshProfileRepository _profiles;
    private readonly ISshSessionRuntime? _sessions;
    private readonly IPortForwardProfileRepository? _portForwardProfiles;
    private readonly IPortForwardRuntime? _portForwardRuntime;
    private readonly ITransferTaskStore? _transferTasks;
    private readonly ITransferStateStore? _transferStates;

    public ProfileAppService(
        ISshProfileRepository profiles,
        ISshSessionRuntime? sessions = null,
        IPortForwardProfileRepository? portForwardProfiles = null,
        IPortForwardRuntime? portForwardRuntime = null,
        ITransferTaskStore? transferTasks = null,
        ITransferStateStore? transferStates = null)
    {
        _profiles = profiles;
        _sessions = sessions;
        _portForwardProfiles = portForwardProfiles;
        _portForwardRuntime = portForwardRuntime;
        _transferTasks = transferTasks;
        _transferStates = transferStates;
    }

    public Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken)
    {
        return _profiles.ListAsync(cancellationToken);
    }

    public Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken)
    {
        return _profiles.GetAsync(id, cancellationToken);
    }

    public async Task<OperationResult> SaveAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile name is required."));
        }

        if (string.IsNullOrWhiteSpace(profile.Endpoint.Host.Value))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile host is required."));
        }

        if (profile.Endpoint.Port <= 0 || profile.Endpoint.Port > 65535)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile port must be between 1 and 65535.",
                profile.Endpoint.Port.ToString()));
        }

        if (string.IsNullOrWhiteSpace(profile.UserName))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile user name is required."));
        }

        if (profile.AuthMethod == SshAuthMethod.Agent)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH agent authentication is reserved but not enabled in this release."));
        }

        if (profile.AuthMethod is SshAuthMethod.Password
            or SshAuthMethod.PrivateKey
            or SshAuthMethod.PrivateKeyWithPassphrase
            or SshAuthMethod.KeyboardInteractive
            && profile.CredentialRef is null)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile credential reference is required for the selected authentication method."));
        }

        if (profile.TerminalProfile is not null)
        {
            if (profile.TerminalProfile.FontSize <= 0)
            {
                return OperationResult.Failure(new SshError(
                    SshErrorKind.Validation,
                    "Terminal font size must be greater than zero."));
            }

            if (profile.TerminalProfile.InitialSize.Columns <= 0 || profile.TerminalProfile.InitialSize.Rows <= 0)
            {
                return OperationResult.Failure(new SshError(
                    SshErrorKind.Validation,
                    "Terminal initial size must be greater than zero."));
            }
        }

        if (profile.JumpHostProfileId is not null)
        {
            if (profile.JumpHostProfileId.Value == profile.Id)
            {
                return OperationResult.Failure(new SshError(
                    SshErrorKind.Validation,
                    "SSH profile cannot use itself as a jump host.",
                    profile.Id.Value.ToString()));
            }

            var jumpHost = await _profiles.GetAsync(profile.JumpHostProfileId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (!jumpHost.Succeeded)
            {
                return OperationResult.Failure(jumpHost.Error!);
            }

            if (jumpHost.Value is null)
            {
                return OperationResult.Failure(new SshError(
                    SshErrorKind.Validation,
                    "SSH profile jump host was not found.",
                    profile.JumpHostProfileId.Value.Value.ToString()));
            }
        }

        return await _profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteAsync(SshProfileId id, CancellationToken cancellationToken)
    {
        var referenceCheck = await EnsureProfileIsNotReferencedAsync(id, cancellationToken).ConfigureAwait(false);
        if (!referenceCheck.Succeeded)
        {
            return referenceCheck;
        }

        return await _profiles.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult> EnsureProfileIsNotReferencedAsync(
        SshProfileId id,
        CancellationToken cancellationToken)
    {
        var profiles = await _profiles.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!profiles.Succeeded)
        {
            return OperationResult.Failure(profiles.Error!);
        }

        if (profiles.Value!.Any(profile => profile.JumpHostProfileId == id))
        {
            return Referenced("SSH profile is used as a jump host by another profile.", id);
        }

        if (_sessions is not null)
        {
            var sessions = await _sessions.ListSnapshotsAsync(cancellationToken).ConfigureAwait(false);
            if (!sessions.Succeeded)
            {
                return OperationResult.Failure(sessions.Error!);
            }

            if (sessions.Value!.Any(session => session.ProfileId == id && session.State is not SshSessionState.Disconnected))
            {
                return Referenced("SSH profile has active sessions.", id);
            }
        }

        if (_portForwardProfiles is not null)
        {
            var forwards = await _portForwardProfiles.ListAsync(cancellationToken).ConfigureAwait(false);
            if (!forwards.Succeeded)
            {
                return OperationResult.Failure(forwards.Error!);
            }

            if (forwards.Value!.Any(forward => forward.ProfileId == id))
            {
                return Referenced("SSH profile is used by port forwarding profiles.", id);
            }
        }

        if (_portForwardRuntime is not null)
        {
            var statuses = await _portForwardRuntime.ListAsync(cancellationToken).ConfigureAwait(false);
            if (!statuses.Succeeded)
            {
                return OperationResult.Failure(statuses.Error!);
            }

            if (statuses.Value!.Any(status => status.ProfileId == id && status.State is PortForwardState.Starting or PortForwardState.Running))
            {
                return Referenced("SSH profile has active port forwarding instances.", id);
            }
        }

        if (_transferTasks is not null)
        {
            var sftpTasks = await _transferTasks.ListSftpAsync(cancellationToken).ConfigureAwait(false);
            if (!sftpTasks.Succeeded)
            {
                return OperationResult.Failure(sftpTasks.Error!);
            }

            var remoteCopyTasks = await _transferTasks.ListRemoteCopyAsync(cancellationToken).ConfigureAwait(false);
            if (!remoteCopyTasks.Succeeded)
            {
                return OperationResult.Failure(remoteCopyTasks.Error!);
            }

            var activeTaskIds = await GetActiveTransferTaskIdsAsync(cancellationToken).ConfigureAwait(false);
            if (!activeTaskIds.Succeeded)
            {
                return OperationResult.Failure(activeTaskIds.Error!);
            }

            if (sftpTasks.Value!.Any(task => task.ProfileId == id && IsTransferBlocking(task.Id, task.Status, activeTaskIds.Value!))
                || remoteCopyTasks.Value!.Any(task =>
                    (task.SourceProfileId == id || task.TargetProfileId == id)
                    && IsTransferBlocking(task.Id, task.Status, activeTaskIds.Value!)))
            {
                return Referenced("SSH profile is used by unfinished transfer tasks.", id);
            }
        }

        return OperationResult.Success();
    }

    private async Task<OperationResult<HashSet<TransferTaskId>>> GetActiveTransferTaskIdsAsync(CancellationToken cancellationToken)
    {
        if (_transferStates is null)
        {
            return OperationResult<HashSet<TransferTaskId>>.Success([]);
        }

        var states = await _transferStates.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!states.Succeeded)
        {
            return OperationResult<HashSet<TransferTaskId>>.Failure(states.Error!);
        }

        return OperationResult<HashSet<TransferTaskId>>.Success(states.Value!
            .Where(progress => progress.Status is TransferStatus.Pending or TransferStatus.Running or TransferStatus.Interrupted)
            .Select(progress => progress.TaskId)
            .ToHashSet());
    }

    private static bool IsTransferBlocking(
        TransferTaskId taskId,
        TransferStatus taskStatus,
        HashSet<TransferTaskId> activeTaskIds)
    {
        return taskStatus is TransferStatus.Pending or TransferStatus.Running or TransferStatus.Interrupted
            || activeTaskIds.Contains(taskId);
    }

    private static OperationResult Referenced(string message, SshProfileId id)
    {
        return OperationResult.Failure(new SshError(
            SshErrorKind.Validation,
            message,
            id.Value.ToString()));
    }
}
