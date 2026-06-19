using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Transfer.Scheduling;

public sealed class FakeTransferTaskScheduler : ITransferTaskScheduler
{
    private readonly ITransferStateStore _stateStore;

    public FakeTransferTaskScheduler(ITransferStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public Task<OperationResult> SubmitAsync(
        SftpTransferTask task,
        TransferExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        return SaveSucceededProgressAsync(task.Id, cancellationToken);
    }

    public Task<OperationResult> SubmitAsync(
        RemoteCopyTask task,
        TransferExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        return SaveSucceededProgressAsync(task.Id, cancellationToken);
    }

    public Task<OperationResult> RetryAsync(
        SftpTransferTask task,
        TransferExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(task, executionPlan, cancellationToken);
    }

    public Task<OperationResult> RetryAsync(
        RemoteCopyTask task,
        TransferExecutionPlan executionPlan,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(task, executionPlan, cancellationToken);
    }

    public async Task<OperationResult> CancelAsync(TransferTaskId taskId, CancellationToken cancellationToken)
    {
        var progress = new TransferProgress(taskId, 0, null, null, TransferStatus.Cancelled);
        return await _stateStore.SaveAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult> SaveSucceededProgressAsync(
        TransferTaskId taskId,
        CancellationToken cancellationToken)
    {
        var progress = new TransferProgress(taskId, 1, 1, null, TransferStatus.Succeeded);
        return await _stateStore.SaveAsync(progress, cancellationToken).ConfigureAwait(false);
    }
}
