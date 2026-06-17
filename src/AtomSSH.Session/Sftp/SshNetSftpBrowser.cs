using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Sftp;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Session.Errors;
using Renci.SshNet.Sftp;

namespace AtomSSH.Session.Sftp;

internal sealed class SshNetSftpBrowser : ISftpBrowser
{
    private readonly SshNetSftpClientFactory _clientFactory;

    public SshNetSftpBrowser(SshNetSftpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<OperationResult<IReadOnlyList<SftpItem>>> ListAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        var clientResult = await _clientFactory.CreateConnectedAsync(profile, route, cancellationToken)
            .ConfigureAwait(false);
        if (!clientResult.Succeeded || clientResult.Value is null)
        {
            return OperationResult<IReadOnlyList<SftpItem>>.Failure(clientResult.Error!);
        }

        using var connection = clientResult.Value;
        var client = connection.Client;
        try
        {
            var items = client
                .ListDirectory(path.Value)
                .Where(file => file.Name is not "." and not "..")
                .Select(ToSftpItem)
                .ToArray();

            return OperationResult<IReadOnlyList<SftpItem>>.Success(items);
        }
        catch (Exception exception)
        {
            return OperationResult<IReadOnlyList<SftpItem>>.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    public async Task<OperationResult> DeleteAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        var clientResult = await _clientFactory.CreateConnectedAsync(profile, route, cancellationToken)
            .ConfigureAwait(false);
        if (!clientResult.Succeeded || clientResult.Value is null)
        {
            return OperationResult.Failure(clientResult.Error!);
        }

        using var connection = clientResult.Value;
        var client = connection.Client;
        try
        {
            var attributes = client.GetAttributes(path.Value);
            if (attributes.IsDirectory)
            {
                client.DeleteDirectory(path.Value);
            }
            else
            {
                client.DeleteFile(path.Value);
            }

            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(SshNetErrorMapper.Map(exception));
        }
    }

    private static SftpItem ToSftpItem(ISftpFile file)
    {
        return new SftpItem(
            new RemotePath(file.FullName),
            file.Name,
            ToKind(file),
            file.Length,
            file.LastWriteTimeUtc,
            string.Empty);
    }

    private static SftpItemKind ToKind(ISftpFile file)
    {
        if (file.IsDirectory)
        {
            return SftpItemKind.Directory;
        }

        if (file.IsSymbolicLink)
        {
            return SftpItemKind.SymbolicLink;
        }

        return file.IsRegularFile ? SftpItemKind.File : SftpItemKind.Other;
    }
}
