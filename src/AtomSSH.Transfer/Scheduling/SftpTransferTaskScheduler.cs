using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Transfer.Scheduling;

public sealed class SftpTransferTaskScheduler : ITransferTaskScheduler
{
    private readonly ISshProfileRepository _profiles;
    private readonly ISftpFileTransfer _fileTransfer;
    private readonly ITransferStateStore _stateStore;

    public SftpTransferTaskScheduler(
        ISshProfileRepository profiles,
        ISftpFileTransfer fileTransfer,
        ITransferStateStore stateStore)
    {
        _profiles = profiles;
        _fileTransfer = fileTransfer;
        _stateStore = stateStore;
    }

    public async Task<OperationResult> SubmitAsync(
        SftpTransferTask task,
        TransferExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        var running = await SaveProgressAsync(
            task.Id,
            0,
            null,
            TransferStatus.Running,
            null,
            cancellationToken).ConfigureAwait(false);
        if (!running.Succeeded)
        {
            return running;
        }

        var profileResult = await _profiles.GetAsync(task.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!profileResult.Succeeded || profileResult.Value is null)
        {
            var error = profileResult.Error ?? new SshError(
                SshErrorKind.Validation,
                "SSH profile was not found.",
                task.ProfileId.Value.ToString());
            await SaveProgressAsync(task.Id, 0, null, TransferStatus.Failed, error, cancellationToken)
                .ConfigureAwait(false);
            return OperationResult.Failure(error);
        }

        OperationResult<long> transferResult;
        try
        {
            transferResult = task.Direction == TransferDirection.Upload
                ? await _fileTransfer.UploadAsync(
                    profileResult.Value,
                    executionPlan.SourceRoute,
                    task.LocalPath,
                    task.RemotePath,
                    task.OverwritePolicy,
                    cancellationToken).ConfigureAwait(false)
                : await _fileTransfer.DownloadAsync(
                    profileResult.Value,
                    executionPlan.SourceRoute,
                    task.RemotePath,
                    task.LocalPath,
                    task.OverwritePolicy,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            var error = new SshError(
                SshErrorKind.Cancelled,
                "SFTP transfer was cancelled.",
                SshErrorRedactor.RedactDetail(exception.Message));
            await SaveProgressAsync(task.Id, 0, null, TransferStatus.Cancelled, error, CancellationToken.None)
                .ConfigureAwait(false);
            return OperationResult.Failure(error);
        }
        catch (Exception exception)
        {
            var error = new SshError(
                SshErrorKind.Internal,
                "SFTP transfer failed.",
                SshErrorRedactor.RedactDetail(exception.Message));
            await SaveProgressAsync(task.Id, 0, null, TransferStatus.Failed, error, CancellationToken.None)
                .ConfigureAwait(false);
            return OperationResult.Failure(error);
        }

        if (!transferResult.Succeeded)
        {
            await SaveProgressAsync(
                task.Id,
                0,
                null,
                TransferStatus.Failed,
                transferResult.Error,
                cancellationToken).ConfigureAwait(false);
            return OperationResult.Failure(transferResult.Error!);
        }

        return await SaveProgressAsync(
            task.Id,
            transferResult.Value,
            transferResult.Value,
            TransferStatus.Succeeded,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SubmitAsync(
        RemoteCopyTask task,
        TransferExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        var running = await SaveProgressAsync(
            task.Id,
            0,
            null,
            TransferStatus.Running,
            null,
            cancellationToken).ConfigureAwait(false);
        if (!running.Succeeded)
        {
            return running;
        }

        if (task.Mode != RemoteCopyMode.LocalRelay)
        {
            return await FailAsync(
                task.Id,
                new SshError(
                    SshErrorKind.Validation,
                    "Only LocalRelay remote copy mode is implemented in the first transfer scheduler."),
                cancellationToken).ConfigureAwait(false);
        }

        if (executionPlan.TargetRoute is null)
        {
            return await FailAsync(
                task.Id,
                new SshError(
                    SshErrorKind.Validation,
                    "Remote copy execution requires both source and target routes."),
                cancellationToken).ConfigureAwait(false);
        }

        var sourceProfile = await _profiles.GetAsync(task.SourceProfileId, cancellationToken).ConfigureAwait(false);
        if (!sourceProfile.Succeeded || sourceProfile.Value is null)
        {
            return await FailAsync(
                task.Id,
                sourceProfile.Error ?? new SshError(
                    SshErrorKind.Validation,
                    "Source SSH profile was not found.",
                    task.SourceProfileId.Value.ToString()),
                cancellationToken).ConfigureAwait(false);
        }

        var targetProfile = await _profiles.GetAsync(task.TargetProfileId, cancellationToken).ConfigureAwait(false);
        if (!targetProfile.Succeeded || targetProfile.Value is null)
        {
            return await FailAsync(
                task.Id,
                targetProfile.Error ?? new SshError(
                    SshErrorKind.Validation,
                    "Target SSH profile was not found.",
                    task.TargetProfileId.Value.ToString()),
                cancellationToken).ConfigureAwait(false);
        }

        var sourceStream = await _fileTransfer.OpenReadAsync(
            sourceProfile.Value,
            executionPlan.SourceRoute,
            task.SourcePath,
            cancellationToken).ConfigureAwait(false);
        if (!sourceStream.Succeeded || sourceStream.Value is null)
        {
            return await FailAsync(task.Id, sourceStream.Error!, cancellationToken).ConfigureAwait(false);
        }

        await using var sourceLease = sourceStream.Value;
        var targetStream = await _fileTransfer.OpenWriteAsync(
            targetProfile.Value,
            executionPlan.TargetRoute,
            task.TargetPath,
            task.OverwritePolicy,
            cancellationToken).ConfigureAwait(false);
        if (!targetStream.Succeeded || targetStream.Value is null)
        {
            return await FailAsync(task.Id, targetStream.Error!, cancellationToken).ConfigureAwait(false);
        }

        await using var targetLease = targetStream.Value;
        try
        {
            await sourceLease.Stream.CopyToAsync(targetLease.Stream, cancellationToken).ConfigureAwait(false);
            await targetLease.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var transferred = sourceLease.Length ?? 0;
            return await SaveProgressAsync(
                task.Id,
                transferred,
                sourceLease.Length,
                TransferStatus.Succeeded,
                null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            var error = new SshError(
                SshErrorKind.Cancelled,
                "Remote copy was cancelled.",
                SshErrorRedactor.RedactDetail(exception.Message));
            await SaveProgressAsync(task.Id, 0, sourceLease.Length, TransferStatus.Cancelled, error, CancellationToken.None)
                .ConfigureAwait(false);
            return OperationResult.Failure(error);
        }
        catch (Exception exception)
        {
            return await FailAsync(
                task.Id,
                new SshError(
                    SshErrorKind.Internal,
                    "Remote copy failed.",
                    SshErrorRedactor.RedactDetail(exception.Message)),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<OperationResult> CancelAsync(TransferTaskId taskId, CancellationToken cancellationToken)
    {
        return SaveProgressAsync(taskId, 0, null, TransferStatus.Cancelled, null, cancellationToken);
    }

    private async Task<OperationResult> SaveProgressAsync(
        TransferTaskId taskId,
        long bytesTransferred,
        long? totalBytes,
        TransferStatus status,
        SshError? error,
        CancellationToken cancellationToken)
    {
        var progress = new TransferProgress(taskId, bytesTransferred, totalBytes, null, status, error);
        return await _stateStore.SaveAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult> FailAsync(
        TransferTaskId taskId,
        SshError error,
        CancellationToken cancellationToken)
    {
        await SaveProgressAsync(taskId, 0, null, TransferStatus.Failed, error, cancellationToken)
            .ConfigureAwait(false);
        return OperationResult.Failure(error);
    }
}
