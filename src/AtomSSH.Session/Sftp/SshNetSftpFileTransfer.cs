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
            var finalRemotePath = overwritePolicy == TransferOverwritePolicy.Rename
                ? CreateUniqueRemotePath(client.Exists, remotePath.Value)
                : remotePath.Value;
            var remoteExists = client.Exists(finalRemotePath);
            if (remoteExists && overwritePolicy == TransferOverwritePolicy.FailIfExists)
            {
                return OperationResult<long>.Failure(new SshError(
                    SshErrorKind.Path,
                    "Remote file already exists.",
                    finalRemotePath));
            }

            await using var stream = new FileStream(
                localPath.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            await using var remoteStream = client.OpenWrite(finalRemotePath);
            await stream.CopyToAsync(remoteStream, cancellationToken).ConfigureAwait(false);
            await remoteStream.FlushAsync(cancellationToken).ConfigureAwait(false);

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
            var finalLocalPath = overwritePolicy == TransferOverwritePolicy.Rename
                ? CreateUniqueLocalPath(localPath.Value)
                : localPath.Value;
            var directory = Path.GetDirectoryName(finalLocalPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var attributes = client.GetAttributes(remotePath.Value);
            var mode = overwritePolicy == TransferOverwritePolicy.Overwrite
                ? FileMode.Create
                : FileMode.CreateNew;
            await using var stream = new FileStream(
                finalLocalPath,
                mode,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await using var remoteStream = client.OpenRead(remotePath.Value);
            await remoteStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return OperationResult<long>.Success(attributes.Size);
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
        var clientResult = await _clientFactory.CreateConnectedAsync(profile, route, cancellationToken)
            .ConfigureAwait(false);
        if (!clientResult.Succeeded || clientResult.Value is null)
        {
            return OperationResult<ISftpFileStreamLease>.Failure(clientResult.Error!);
        }

        var connection = clientResult.Value;
        try
        {
            var finalRemotePath = overwritePolicy == TransferOverwritePolicy.Rename
                ? CreateUniqueRemotePath(connection.Client.Exists, remotePath.Value)
                : remotePath.Value;

            if (connection.Client.Exists(finalRemotePath)
                && overwritePolicy == TransferOverwritePolicy.FailIfExists)
            {
                connection.Dispose();
                return OperationResult<ISftpFileStreamLease>.Failure(new SshError(
                    SshErrorKind.Path,
                    "Remote file already exists.",
                    finalRemotePath));
            }

            var stream = connection.Client.OpenWrite(finalRemotePath);
            return OperationResult<ISftpFileStreamLease>.Success(
                new SshNetSftpFileStreamLease(connection, stream, null));
        }
        catch (Exception exception)
        {
            connection.Dispose();
            return OperationResult<ISftpFileStreamLease>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    private static string CreateUniqueLocalPath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory ?? string.Empty, $"{name} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not create a unique local path for {path}.");
    }

    private static string CreateUniqueRemotePath(Func<string, bool> exists, string path)
    {
        if (!exists(path))
        {
            return path;
        }

        var slashIndex = path.LastIndexOf('/');
        var directory = slashIndex >= 0 ? path[..slashIndex] : string.Empty;
        var fileName = slashIndex >= 0 ? path[(slashIndex + 1)..] : path;
        var dotIndex = fileName.LastIndexOf('.');
        var name = dotIndex > 0 ? fileName[..dotIndex] : fileName;
        var extension = dotIndex > 0 ? fileName[dotIndex..] : string.Empty;

        for (var index = 1; index < 10_000; index++)
        {
            var candidateName = $"{name} ({index}){extension}";
            var candidate = string.IsNullOrEmpty(directory)
                ? candidateName
                : $"{directory}/{candidateName}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not create a unique remote path for {path}.");
    }
}
