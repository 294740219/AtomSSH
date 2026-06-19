using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Transfers;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.Transfers;

public sealed class JsonTransferTaskStore : ITransferTaskStore
{
    private readonly JsonFileStore<TransferTaskEnvelope> _store;

    public JsonTransferTaskStore(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<TransferTaskEnvelope>(directory.TransferTasksFile);
    }

    public async Task<OperationResult<IReadOnlyList<SftpTransferTask>>> ListSftpAsync(CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        return !envelope.Succeeded
            ? OperationResult<IReadOnlyList<SftpTransferTask>>.Failure(envelope.Error!)
            : OperationResult<IReadOnlyList<SftpTransferTask>>.Success(envelope.Value!.SftpTasks);
    }

    public async Task<OperationResult<IReadOnlyList<RemoteCopyTask>>> ListRemoteCopyAsync(CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        return !envelope.Succeeded
            ? OperationResult<IReadOnlyList<RemoteCopyTask>>.Failure(envelope.Error!)
            : OperationResult<IReadOnlyList<RemoteCopyTask>>.Success(envelope.Value!.RemoteCopyTasks);
    }

    public async Task<OperationResult> SaveAsync(SftpTransferTask task, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new TransferTaskEnvelope(),
            envelope =>
            {
                envelope.SftpTasks.RemoveAll(item => item.Id == task.Id);
                envelope.SftpTasks.Add(task);
                return OperationResult<TransferTaskEnvelope>.Success(envelope);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SaveAsync(RemoteCopyTask task, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new TransferTaskEnvelope(),
            envelope =>
            {
                envelope.RemoteCopyTasks.RemoveAll(item => item.Id == task.Id);
                envelope.RemoteCopyTasks.Add(task);
                return OperationResult<TransferTaskEnvelope>.Success(envelope);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private Task<OperationResult<TransferTaskEnvelope>> ReadEnvelopeAsync(CancellationToken cancellationToken)
    {
        return _store.ReadAsync(new TransferTaskEnvelope(), cancellationToken);
    }
}

public sealed class JsonTransferStateStore : ITransferStateStore
{
    private readonly JsonFileStore<List<TransferProgress>> _store;

    public JsonTransferStateStore(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<List<TransferProgress>>(directory.TransferStateFile);
    }

    public async Task<OperationResult<IReadOnlyList<TransferProgress>>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await _store.ReadAsync(new List<TransferProgress>(), cancellationToken).ConfigureAwait(false);
        return !list.Succeeded
            ? OperationResult<IReadOnlyList<TransferProgress>>.Failure(list.Error!)
            : OperationResult<IReadOnlyList<TransferProgress>>.Success(list.Value!);
    }

    public async Task<OperationResult> SaveAsync(TransferProgress progress, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new List<TransferProgress>(),
            list =>
            {
                list.RemoveAll(item => item.TaskId == progress.TaskId);
                list.Add(progress);
                return OperationResult<List<TransferProgress>>.Success(list);
            },
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed record TransferTaskEnvelope
{
    public List<SftpTransferTask> SftpTasks { get; init; } = new();

    public List<RemoteCopyTask> RemoteCopyTasks { get; init; } = new();
}
