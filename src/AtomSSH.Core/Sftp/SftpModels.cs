using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Sftp;

public sealed record SftpItem(
    RemotePath Path,
    string Name,
    SftpItemKind Kind,
    long Size,
    DateTimeOffset ModifiedAt,
    string Permissions);

public enum SftpItemKind
{
    File,
    Directory,
    SymbolicLink,
    Other
}
