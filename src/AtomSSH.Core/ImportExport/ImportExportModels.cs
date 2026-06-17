namespace AtomSSH.Core.ImportExport;

public sealed record ImportExportPackage(string Version, DateTimeOffset CreatedAt, IReadOnlyDictionary<string, string> Sections);

public sealed record ImportConflict(string Section, string Key, ImportConflictKind Kind);

public enum ImportConflictKind
{
    AlreadyExists,
    InvalidReference,
    UnsupportedVersion
}
