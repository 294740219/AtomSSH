using AtomSSH.Core.CommandSnippets;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
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
        var list = await _store.ReadAsync(new List<CommandSnippet>(), cancellationToken).ConfigureAwait(false);
        if (!list.Succeeded)
        {
            return OperationResult.Failure(list.Error!);
        }

        list.Value!.RemoveAll(item => item.Id == snippet.Id);
        list.Value.Add(snippet);
        return await _store.WriteAsync(list.Value, cancellationToken).ConfigureAwait(false);
    }
}
