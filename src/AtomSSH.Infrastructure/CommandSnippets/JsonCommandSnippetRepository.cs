using AtomSSH.Core.CommandSnippets;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.CommandSnippets;

public sealed class JsonCommandSnippetRepository : ICommandSnippetRepository
{
    private readonly JsonFileStore<List<CommandSnippet>> _store;

    public JsonCommandSnippetRepository(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<List<CommandSnippet>>(directory.CommandSnippetsFile);
    }

    public async Task<OperationResult<IReadOnlyList<CommandSnippet>>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await _store.ReadAsync(new List<CommandSnippet>(), cancellationToken).ConfigureAwait(false);
        return !list.Succeeded
            ? OperationResult<IReadOnlyList<CommandSnippet>>.Failure(list.Error!)
            : OperationResult<IReadOnlyList<CommandSnippet>>.Success(list.Value!);
    }

    public async Task<OperationResult> SaveAsync(CommandSnippet snippet, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new List<CommandSnippet>(),
            list =>
            {
                list.RemoveAll(item => item.Id == snippet.Id);
                list.Add(snippet);
                return OperationResult<List<CommandSnippet>>.Success(list);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteAsync(CommandSnippetId id, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new List<CommandSnippet>(),
            list =>
            {
                list.RemoveAll(item => item.Id == id);
                return OperationResult<List<CommandSnippet>>.Success(list);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
