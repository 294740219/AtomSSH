using AtomSSH.Core.Network;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Session.Connections;
using Renci.SshNet;

namespace AtomSSH.Session.Sftp;

internal sealed class SshNetSftpClientFactory
{
    private readonly SshNetClientConnector _connector;

    public SshNetSftpClientFactory(SshNetClientConnector connector)
    {
        _connector = connector;
    }

    public Task<OperationResult<SshNetClientConnection<SftpClient>>> CreateConnectedAsync(
        SshProfile profile,
        ConnectionRoute route,
        CancellationToken cancellationToken)
    {
        return _connector.ConnectSftpClientAsync(profile, route, cancellationToken);
    }
}
