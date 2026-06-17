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

    public async Task<OperationResult> SaveAsync(SftpTransferTask task, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        if (!envelope.Succeeded)
        {
            return OperationResult.Failure(envelope.Error!);
        }

        envelope.Value!.SftpTasks.RemoveAll(item => item.Id == task.Id);
        envelope.Value.SftpTasks.Add(task);
        return await _store.WriteAsync(envelope.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SaveAsync(RemoteCopyTask task, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
        if (!envelope.Succeeded)
        {
            return OperationResult.Failure(envelope.Error!);
        }

        envelope.Value!.RemoteCopyTasks.RemoveAll(item => item.Id == task.Id);
        envelope.Value.RemoteCopyTasks.Add(task);
        return await _store.WriteAsync(envelope.Value, cancellationToken).ConfigureAwait(false);
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
        var list = await _store.ReadAsync(new List<TransferProgress>(), cancellationToken).ConfigureAwait(false);
        if (!list.Succeeded)
        {
            return OperationResult.Failure(list.Error!);
        }

        list.Value!.RemoveAll(item => item.TaskId == progress.TaskId);
        list.Value.Add(progress);
        return await _store.WriteAsync(list.Value, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record TransferTaskEnvelope
{
    public List<SftpTransferTask> SftpTasks { get; init; } = new();

    public List<RemoteCopyTask> RemoteCopyTasks { get; init; } = new();
}
