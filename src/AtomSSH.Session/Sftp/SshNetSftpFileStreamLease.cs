using AtomSSH.Core.Ports;
using AtomSSH.Session.Connections;
using Renci.SshNet;

namespace AtomSSH.Session.Sftp;

internal sealed class SshNetSftpFileStreamLease : ISftpFileStreamLease
{
    private readonly SshNetClientConnection<SftpClient> _connection;

    public SshNetSftpFileStreamLease(
        SshNetClientConnection<SftpClient> connection,
        Stream stream,
        long? length)
    {
        _connection = connection;
        Stream = stream;
        Length = length;
    }

    public Stream Stream { get; }

    public long? Length { get; }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync().ConfigureAwait(false);
        _connection.Dispose();
    }
}
