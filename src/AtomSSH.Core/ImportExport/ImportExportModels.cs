namespace AtomSSH.Core.ImportExport;

public sealed record ImportExportPackage(string Version, DateTimeOffset CreatedAt, IReadOnlyDictionary<string, string> Sections);

public sealed record ImportConflict(string Section, string Key, ImportConflictKind Kind);

public sealed record ImportResult(IReadOnlyList<ImportConflict> Conflicts, int ImportedSections);

public sealed record ImportOptions(ImportConflictResolution ConflictResolution);

public enum ImportConflictKind
{
    AlreadyExists,
    InvalidReference,
    UnsupportedVersion
}

public enum ImportConflictResolution
{
    FailOnConflict,
    Overwrite
}
