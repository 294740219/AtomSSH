using AtomSSH.Core.Credentials;
using AtomSSH.Core.Network;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Sftp;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Ports;

public interface ICredentialResolver
{
    Task<OperationResult<CredentialLease>> ResolveAsync(CredentialRef credentialRef, CancellationToken cancellationToken);
}

public interface ISshSessionRuntime
{
    Task<OperationResult<SshSessionInstanceId>> OpenTerminalAsync(SshProfile profile, ConnectionRoute route, CancellationToken cancellationToken);

    Task<OperationResult<ITerminalChannel>> GetTerminalChannelAsync(SshSessionInstanceId sessionId, CancellationToken cancellationToken);

    Task<OperationResult> CloseAsync(SshSessionInstanceId sessionId, CancellationToken cancellationToken);
}

public interface ISshSessionFactory
{
    Task<OperationResult<SshSessionInstanceId>> OpenAsync(SshProfile profile, ConnectionRoute route, CancellationToken cancellationToken);
}

public interface ITerminalChannel
{
    SshSessionInstanceId SessionId { get; }

    Task<OperationResult<int>> ReadAsync(Memory<byte> output, CancellationToken cancellationToken);

    Task<OperationResult> SendAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken);

    Task<OperationResult> ResizeAsync(TerminalSize size, CancellationToken cancellationToken);
}

public interface ISftpBrowser
{
    Task<OperationResult<IReadOnlyList<SftpItem>>> ListAsync(SshProfile profile, ConnectionRoute route, RemotePath path, CancellationToken cancellationToken);

    Task<OperationResult> DeleteAsync(SshProfile profile, ConnectionRoute route, RemotePath path, CancellationToken cancellationToken);
}

public interface ISftpFileTransfer
{
    Task<OperationResult<long>> UploadAsync(
        SshProfile profile,
        ConnectionRoute route,
        LocalPath localPath,
        RemotePath remotePath,
        TransferOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken);

    Task<OperationResult<long>> DownloadAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath remotePath,
        LocalPath localPath,
        TransferOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken);

    Task<OperationResult<ISftpFileStreamLease>> OpenReadAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath remotePath,
        CancellationToken cancellationToken);

    Task<OperationResult<ISftpFileStreamLease>> OpenWriteAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath remotePath,
        TransferOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken);
}

public interface ISftpFileStreamLease : IAsyncDisposable
{
    Stream Stream { get; }

    long? Length { get; }
}

public interface IPortForwardRuntime
{
    Task<OperationResult<PortForwardInstanceId>> StartAsync(PortForwardProfile profile, ConnectionRoute route, CancellationToken cancellationToken);

    Task<OperationResult> StopAsync(PortForwardInstanceId instanceId, CancellationToken cancellationToken);
}

public interface IConnectionRoutePlanner
{
    Task<OperationResult<ConnectionRoute>> PlanAsync(SshProfile profile, CancellationToken cancellationToken);
}

public interface INetworkDiagnosticsService
{
    Task<OperationResult<NetworkDiagnosticResult>> DiagnoseAsync(ConnectionRoute route, CancellationToken cancellationToken);
}

public interface ITransferTaskScheduler
{
    Task<OperationResult> SubmitAsync(SftpTransferTask task, TransferExecutionPlan executionPlan, CancellationToken cancellationToken);

    Task<OperationResult> SubmitAsync(RemoteCopyTask task, TransferExecutionPlan executionPlan, CancellationToken cancellationToken);

    Task<OperationResult> CancelAsync(TransferTaskId taskId, CancellationToken cancellationToken);
}
