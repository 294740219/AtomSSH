using AtomSSH.Core.Network;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Transfers;

public sealed record SftpTransferTask(
    TransferTaskId Id,
    SshProfileId ProfileId,
    TransferDirection Direction,
    LocalPath LocalPath,
    RemotePath RemotePath,
    TransferOverwritePolicy OverwritePolicy,
    DateTimeOffset CreatedAt,
    TransferStatus Status);

public sealed record RemoteCopyTask(
    TransferTaskId Id,
    SshProfileId SourceProfileId,
    SshProfileId TargetProfileId,
    RemotePath SourcePath,
    RemotePath TargetPath,
    RemoteCopyMode Mode,
    TransferOverwritePolicy OverwritePolicy,
    DateTimeOffset CreatedAt,
    TransferStatus Status);

public sealed record TransferExecutionPlan(
    TransferTaskId TaskId,
    ConnectionRoute SourceRoute,
    ConnectionRoute? TargetRoute = null);

public sealed record TransferProgress(
    TransferTaskId TaskId,
    long BytesTransferred,
    long? TotalBytes,
    double? BytesPerSecond,
    TransferStatus Status,
    SshError? LastError = null);

public enum TransferDirection
{
    Upload,
    Download
}

public enum TransferOverwritePolicy
{
    FailIfExists,
    Overwrite,
    Rename
}

public enum RemoteCopyMode
{
    LocalRelay,
    RemoteCommand
}

public enum TransferStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Interrupted
}
