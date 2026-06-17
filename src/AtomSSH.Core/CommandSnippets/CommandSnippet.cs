using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.CommandSnippets;

public sealed record CommandSnippet(CommandSnippetId Id, string Name, string CommandText, string? Group = null);
