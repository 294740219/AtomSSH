using AtomSSH.Core.CommandSnippets;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
using System.Text;

namespace AtomSSH.Application.CommandSnippets;

public sealed class CommandSnippetAppService
{
    private readonly ICommandSnippetRepository _snippets;
    private readonly ISshSessionRuntime _sessions;

    public CommandSnippetAppService(ICommandSnippetRepository snippets, ISshSessionRuntime sessions)
    {
        _snippets = snippets;
        _sessions = sessions;
    }

    public Task<OperationResult<IReadOnlyList<CommandSnippet>>> ListAsync(CancellationToken cancellationToken)
    {
        return _snippets.ListAsync(cancellationToken);
    }

    public Task<OperationResult> SaveAsync(CommandSnippet snippet, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snippet.Name))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Command snippet name is required.")));
        }

        if (string.IsNullOrWhiteSpace(snippet.CommandText))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Command snippet text is required.")));
        }

        return _snippets.SaveAsync(snippet, cancellationToken);
    }

    public async Task<OperationResult> SendAsync(
        CommandSnippetId snippetId,
        SshSessionInstanceId sessionId,
        CancellationToken cancellationToken)
    {
        var snippets = await _snippets.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!snippets.Succeeded)
        {
            return OperationResult.Failure(SshErrorRedactor.Redact(snippets.Error!));
        }

        var snippet = snippets.Value!.FirstOrDefault(item => item.Id == snippetId);
        if (snippet is null)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Command snippet was not found.",
                snippetId.Value.ToString()));
        }

        var channel = await _sessions.GetTerminalChannelAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (!channel.Succeeded || channel.Value is null)
        {
            return OperationResult.Failure(SshErrorRedactor.Redact(channel.Error!));
        }

        var command = snippet.CommandText.EndsWith('\n')
            ? snippet.CommandText
            : snippet.CommandText + "\n";
        return await channel.Value.SendAsync(Encoding.UTF8.GetBytes(command), cancellationToken).ConfigureAwait(false);
    }
}
