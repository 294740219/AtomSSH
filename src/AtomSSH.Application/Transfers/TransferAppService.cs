using AtomSSH.Application.Common;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Application.Transfers;

public sealed class TransferAppService
{
    private readonly ISshProfileRepository _profiles;
    private readonly IConnectionRoutePlanner _routePlanner;
    private readonly ITransferTaskStore _taskStore;
    private readonly ITransferStateStore _stateStore;
    private readonly ITransferTaskScheduler _scheduler;

    public TransferAppService(
        ISshProfileRepository profiles,
        IConnectionRoutePlanner routePlanner,
        ITransferTaskStore taskStore,
        ITransferStateStore stateStore,
        ITransferTaskScheduler scheduler)
    {
        _profiles = profiles;
        _routePlanner = routePlanner;
        _taskStore = taskStore;
        _stateStore = stateStore;
        _scheduler = scheduler;
    }

    public Task<OperationResult<IReadOnlyList<TransferProgress>>> ListStateAsync(CancellationToken cancellationToken)
    {
        return _stateStore.ListAsync(cancellationToken);
    }

    public async Task<OperationResult<TransferTaskId>> CreateSftpTransferAsync(
        SftpTransferTask task,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSftpTask(task);
        if (!validation.Succeeded)
        {
            return OperationResult<TransferTaskId>.Failure(validation.Error!);
        }

        var profile = await _profiles.GetAsync(task.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult<TransferTaskId>.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", task.ProfileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult<TransferTaskId>.Failure(route.Error!);
        }

        var save = await _taskStore.SaveAsync(task, cancellationToken).ConfigureAwait(false);
        if (!save.Succeeded)
        {
            return OperationResult<TransferTaskId>.Failure(save.Error!);
        }

        var submit = await _scheduler.SubmitAsync(
            task,
            new TransferExecutionPlan(task.Id, route.Value),
            cancellationToken).ConfigureAwait(false);

        return submit.Succeeded
            ? OperationResult<TransferTaskId>.Success(task.Id)
            : OperationResult<TransferTaskId>.Failure(submit.Error!);
    }

    public async Task<OperationResult<TransferTaskId>> CreateRemoteCopyAsync(
        RemoteCopyTask task,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRemoteCopyTask(task);
        if (!validation.Succeeded)
        {
            return OperationResult<TransferTaskId>.Failure(validation.Error!);
        }

        var source = await _profiles.GetAsync(task.SourceProfileId, cancellationToken).ConfigureAwait(false);
        if (!source.Succeeded || source.Value is null)
        {
            return OperationResult<TransferTaskId>.Failure(
                source.Error ?? ApplicationErrors.NotFound("Source SSH profile was not found.", task.SourceProfileId.Value.ToString()));
        }

        var target = await _profiles.GetAsync(task.TargetProfileId, cancellationToken).ConfigureAwait(false);
        if (!target.Succeeded || target.Value is null)
        {
            return OperationResult<TransferTaskId>.Failure(
                target.Error ?? ApplicationErrors.NotFound("Target SSH profile was not found.", task.TargetProfileId.Value.ToString()));
        }

        var sourceRoute = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(source.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!sourceRoute.Succeeded || sourceRoute.Value is null)
        {
            return OperationResult<TransferTaskId>.Failure(sourceRoute.Error!);
        }

        var targetRoute = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(target.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!targetRoute.Succeeded || targetRoute.Value is null)
        {
            return OperationResult<TransferTaskId>.Failure(targetRoute.Error!);
        }

        var save = await _taskStore.SaveAsync(task, cancellationToken).ConfigureAwait(false);
        if (!save.Succeeded)
        {
            return OperationResult<TransferTaskId>.Failure(save.Error!);
        }

        var submit = await _scheduler.SubmitAsync(
            task,
            new TransferExecutionPlan(task.Id, sourceRoute.Value, targetRoute.Value),
            cancellationToken).ConfigureAwait(false);

        return submit.Succeeded
            ? OperationResult<TransferTaskId>.Success(task.Id)
            : OperationResult<TransferTaskId>.Failure(submit.Error!);
    }

    public Task<OperationResult> CancelAsync(TransferTaskId taskId, CancellationToken cancellationToken)
    {
        return _scheduler.CancelAsync(taskId, cancellationToken);
    }

    public async Task<OperationResult> RetrySftpTransferAsync(
        SftpTransferTask task,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSftpTask(task);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var profile = await _profiles.GetAsync(task.ProfileId, cancellationToken).ConfigureAwait(false);
        if (!profile.Succeeded || profile.Value is null)
        {
            return OperationResult.Failure(
                profile.Error ?? ApplicationErrors.NotFound("SSH profile was not found.", task.ProfileId.Value.ToString()));
        }

        var route = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(profile.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!route.Succeeded || route.Value is null)
        {
            return OperationResult.Failure(route.Error!);
        }

        return await _scheduler.RetryAsync(task, new TransferExecutionPlan(task.Id, route.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> RetryRemoteCopyAsync(
        RemoteCopyTask task,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRemoteCopyTask(task);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var source = await _profiles.GetAsync(task.SourceProfileId, cancellationToken).ConfigureAwait(false);
        if (!source.Succeeded || source.Value is null)
        {
            return OperationResult.Failure(
                source.Error ?? ApplicationErrors.NotFound("Source SSH profile was not found.", task.SourceProfileId.Value.ToString()));
        }

        var target = await _profiles.GetAsync(task.TargetProfileId, cancellationToken).ConfigureAwait(false);
        if (!target.Succeeded || target.Value is null)
        {
            return OperationResult.Failure(
                target.Error ?? ApplicationErrors.NotFound("Target SSH profile was not found.", task.TargetProfileId.Value.ToString()));
        }

        var sourceRoute = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(source.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!sourceRoute.Succeeded || sourceRoute.Value is null)
        {
            return OperationResult.Failure(sourceRoute.Error!);
        }

        var targetRoute = await _routePlanner.PlanAsync(new ConnectionRoutePlanningRequest(target.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!targetRoute.Succeeded || targetRoute.Value is null)
        {
            return OperationResult.Failure(targetRoute.Error!);
        }

        return await _scheduler.RetryAsync(
                task,
                new TransferExecutionPlan(task.Id, sourceRoute.Value, targetRoute.Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static OperationResult ValidateSftpTask(SftpTransferTask task)
    {
        if (string.IsNullOrWhiteSpace(task.LocalPath.Value))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Local transfer path is required."));
        }

        if (string.IsNullOrWhiteSpace(task.RemotePath.Value))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Remote transfer path is required."));
        }

        return OperationResult.Success();
    }

    private static OperationResult ValidateRemoteCopyTask(RemoteCopyTask task)
    {
        if (task.SourceProfileId == task.TargetProfileId)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Remote copy source and target profiles must be different."));
        }

        if (string.IsNullOrWhiteSpace(task.SourcePath.Value))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Remote copy source path is required."));
        }

        if (string.IsNullOrWhiteSpace(task.TargetPath.Value))
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Remote copy target path is required."));
        }

        return OperationResult.Success();
    }
}
