using AtomSSH.Core.ImportExport;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;

namespace AtomSSH.Application.ImportExport;

public sealed class ImportExportAppService
{
    private readonly IImportExportService _importExport;

    public ImportExportAppService(IImportExportService importExport)
    {
        _importExport = importExport;
    }

    public Task<OperationResult<ImportExportPackage>> ExportAsync(CancellationToken cancellationToken)
    {
        return _importExport.ExportAsync(cancellationToken);
    }

    public Task<OperationResult<IReadOnlyList<ImportConflict>>> PreviewImportAsync(
        ImportExportPackage package,
        CancellationToken cancellationToken)
    {
        return _importExport.PreviewImportAsync(package, cancellationToken);
    }
}
