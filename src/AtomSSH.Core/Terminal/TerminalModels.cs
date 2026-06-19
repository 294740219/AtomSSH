namespace AtomSSH.Core.Terminal;

public sealed record TerminalProfile(
    string FontFamily,
    double FontSize,
    TerminalThemeRef Theme,
    TerminalScrollbackSettings Scrollback,
    string DefaultEncoding,
    TerminalSize InitialSize);

public sealed record TerminalThemeRef(string Name);

public sealed record TerminalScrollbackSettings(int MaxLines);

public sealed record TerminalSize(int Columns, int Rows);

public sealed record SshSessionSnapshot(
    AtomSSH.Core.ValueObjects.SshSessionInstanceId Id,
    AtomSSH.Core.ValueObjects.SshProfileId ProfileId,
    SshSessionState State,
    string? Title = null,
    AtomSSH.Core.Results.SshError? LastError = null);

public enum SshSessionState
{
    Connecting,
    Authenticating,
    VerifyingHostKey,
    Connected,
    Disconnecting,
    Disconnected,
    Failed
}
