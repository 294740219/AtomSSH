using AtomSSH.Core.CommandSnippets;
using AtomSSH.Core.Credentials;
using AtomSSH.Core.Hosts;
using AtomSSH.Core.ImportExport;
using AtomSSH.Core.Network;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Settings;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Ports;

public interface ISshProfileRepository
{
    Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(SshProfile profile, CancellationToken cancellationToken);

    Task<OperationResult> DeleteAsync(SshProfileId id, CancellationToken cancellationToken);
}

public interface IApplicationSettingsRepository
{
    Task<OperationResult<ApplicationSettings>> GetAsync(CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken);
}

public interface IHostKeyTrustStore
{
    Task<OperationResult<KnownHostEntry?>> FindAsync(HostName host, int port, CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(KnownHostEntry entry, CancellationToken cancellationToken);
}

public interface ITransferTaskStore
{
    Task<OperationResult> SaveAsync(SftpTransferTask task, CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(RemoteCopyTask task, CancellationToken cancellationToken);
}

public interface ITransferStateStore
{
    Task<OperationResult<IReadOnlyList<TransferProgress>>> ListAsync(CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(TransferProgress progress, CancellationToken cancellationToken);
}

public interface INetworkInventoryStore
{
    Task<OperationResult<IReadOnlyList<NetworkSpace>>> ListSpacesAsync(CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<NetworkNode>>> ListNodesAsync(Guid networkSpaceId, CancellationToken cancellationToken);

    Task<OperationResult> SaveSpaceAsync(NetworkSpace space, CancellationToken cancellationToken);

    Task<OperationResult> SaveNodeAsync(NetworkNode node, CancellationToken cancellationToken);
}

public interface ICommandSnippetRepository
{
    Task<OperationResult<IReadOnlyList<CommandSnippet>>> ListAsync(CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(CommandSnippet snippet, CancellationToken cancellationToken);
}

public interface IPortForwardProfileRepository
{
    Task<OperationResult<IReadOnlyList<PortForwardProfile>>> ListAsync(CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(PortForwardProfile profile, CancellationToken cancellationToken);
}

public interface ICredentialStore
{
    Task<OperationResult> SaveAsync(CredentialMetadata metadata, CancellationToken cancellationToken);

    Task<OperationResult> SaveAsync(CredentialMetadata metadata, CredentialMaterial material, CancellationToken cancellationToken);

    Task<OperationResult> DeleteAsync(CredentialRef credentialRef, CancellationToken cancellationToken);
}

public interface IImportExportService
{
    Task<OperationResult<ImportExportPackage>> ExportAsync(CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<ImportConflict>>> PreviewImportAsync(ImportExportPackage package, CancellationToken cancellationToken);
}
