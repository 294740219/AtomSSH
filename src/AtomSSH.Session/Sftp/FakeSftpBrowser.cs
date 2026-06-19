using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Sftp;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Session.Sftp;

public sealed class FakeSftpBrowser : ISftpBrowser
{
    public Task<OperationResult<IReadOnlyList<SftpItem>>> ListAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SftpItem> items =
        [
            new SftpItem(new RemotePath(Combine(path, "etc")), "etc", SftpItemKind.Directory, 0, DateTimeOffset.UtcNow, "drwxr-xr-x"),
            new SftpItem(new RemotePath(Combine(path, "app.log")), "app.log", SftpItemKind.File, 1024, DateTimeOffset.UtcNow, "-rw-r--r--")
        ];

        return Task.FromResult(OperationResult<IReadOnlyList<SftpItem>>.Success(items));
    }

    public Task<OperationResult> DeleteAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> CreateDirectoryAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath path,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> RenameAsync(
        SshProfile profile,
        ConnectionRoute route,
        RemotePath sourcePath,
        RemotePath targetPath,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Success());
    }

    private static string Combine(RemotePath path, string name)
    {
        var root = string.IsNullOrWhiteSpace(path.Value) ? "/" : path.Value.TrimEnd('/');
        return root == "/" ? "/" + name : root + "/" + name;
    }
}
