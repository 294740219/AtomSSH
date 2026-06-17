using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Session.Errors;

namespace AtomSSH.Session.Sftp;

internal sealed class SshNetSftpFileTransfer : ISftpFileTransfer
{
    private readonly SshNetSftpClientFactory _clientFactory;

    public SshNetSftpFileTransfer(SshNetSftpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<OperationResult<long>> UploadAsync(
        SshProfile profile,
        ConnectionRoute route,
        LocalPath localPath,
        RemotePath remotePath,
        TransferOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken)
    {
        if (overwritePolicy == TransferOverwritePolicy.Rename)
        {
            return UnsupportedRenamePolicy();
        }

        var fileInfo = new FileInfo(localPath.Value);
        if (!fileInfo.Exists)
        {
            return OperationResult<long>.Failure(new SshError(
                SshErrorKind.Path,
                "Local file was not found.",
                localPath.Value));
        }

        var clientResult = await _clientFactory.CreateConnectedAsync(profile, route, cancellationToken)
            .ConfigureAwait(false);
        if (!clientResult.Succeeded || clientResult.Value is null)
        {
            return OperationResult<long>.Failure(clientResult.Error!);
        }

        using var connection = clientResult.Value;
        var client = connection.Client;
        try
        {
            var remoteExists = client.Exists(remotePath.Value);
            if (remoteExists && overwritePolicy == TransferOverwritePolicy.FailIfExists)
            {
                return OperationResult<long>.Failure(new SshError(
                    SshErrorKind.Path,
                    "Remote file already exists.",
                    remotePath.Value));
            }

            await using var stream = new FileStream(
                localPath.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            client.UploadFile(stream, remotePath.Value, canOverride: overwritePolicy == TransferOverwritePolicy.Overwrite);

            return OperationResult<long>.Success(fileInfo.Length);
        }
        catch (Exception exception)
        {
            return OperationResult<long>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    public async Task<OperationResult<long>> DownloadAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath remotePath,
        LocalPath localPath,
        TransferOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken)
    {
        if (overwritePolicy == TransferOverwritePolicy.Rename)
        {
            return UnsupportedRenamePolicy();
        }

        if (File.Exists(localPath.Value) && overwritePolicy == TransferOverwritePolicy.FailIfExists)
        {
            return OperationResult<long>.Failure(new SshError(
                SshErrorKind.Path,
                "Local file already exists.",
                localPath.Value));
        }

        var clientResult = await _clientFactory.CreateConnectedAsync(profile, route, cancellationToken)
            .ConfigureAwait(false);
        if (!clientResult.Succeeded || clientResult.Value is null)
        {
            return OperationResult<long>.Failure(clientResult.Error!);
        }

        using var connection = clientResult.Value;
        var client = connection.Client;
        try
        {
            var directory = Path.GetDirectoryName(localPath.Value);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var mode = overwritePolicy == TransferOverwritePolicy.Overwrite
                ? FileMode.Create
                : FileMode.CreateNew;
            await using var stream = new FileStream(
                localPath.Value,
                mode,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            client.DownloadFile(remotePath.Value, stream);

            return OperationResult<long>.Success(stream.Length);
        }
        catch (Exception exception)
        {
            return OperationResult<long>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    public async Task<OperationResult<ISftpFileStreamLease>> OpenReadAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath remotePath,
        CancellationToken cancellationToken)
    {
        var clientResult = await _clientFactory.CreateConnectedAsync(profile, route, cancellationToken)
            .ConfigureAwait(false);
        if (!clientResult.Succeeded || clientResult.Value is null)
        {
            return OperationResult<ISftpFileStreamLease>.Failure(clientResult.Error!);
        }

        var connection = clientResult.Value;
        try
        {
            var attributes = connection.Client.GetAttributes(remotePath.Value);
            var stream = connection.Client.OpenRead(remotePath.Value);
            return OperationResult<ISftpFileStreamLease>.Success(
                new SshNetSftpFileStreamLease(connection, stream, attributes.Size));
        }
        catch (Exception exception)
        {
            connection.Dispose();
            return OperationResult<ISftpFileStreamLease>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    public async Task<OperationResult<ISftpFileStreamLease>> OpenWriteAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath remotePath,
        TransferOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken)
    {
        if (overwritePolicy == TransferOverwritePolicy.Rename)
        {
            return OperationResult<ISftpFileStreamLease>.Failure(new SshError(
                SshErrorKind.Validation,
                "Rename overwrite policy is not implemented for SFTP transfer yet."));
        }

        var clientResult = await _clientFactory.CreateConnectedAsync(profile, route, cancellationToken)
            .ConfigureAwait(false);
        if (!clientResult.Succeeded || clientResult.Value is null)
        {
            return OperationResult<ISftpFileStreamLease>.Failure(clientResult.Error!);
        }

        var connection = clientResult.Value;
        try
        {
            if (connection.Client.Exists(remotePath.Value)
                && overwritePolicy == TransferOverwritePolicy.FailIfExists)
            {
                connection.Dispose();
                return OperationResult<ISftpFileStreamLease>.Failure(new SshError(
                    SshErrorKind.Path,
                    "Remote file already exists.",
                    remotePath.Value));
            }

            var stream = connection.Client.OpenWrite(remotePath.Value);
            return OperationResult<ISftpFileStreamLease>.Success(
                new SshNetSftpFileStreamLease(connection, stream, null));
        }
        catch (Exception exception)
        {
            connection.Dispose();
            return OperationResult<ISftpFileStreamLease>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    private static OperationResult<long> UnsupportedRenamePolicy()
    {
        return OperationResult<long>.Failure(new SshError(
            SshErrorKind.Validation,
            "Rename overwrite policy is not implemented for SFTP transfer yet."));
    }
}
