using AtomSSH.Core.Credentials;
using AtomSSH.Core.Network;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using Renci.SshNet;

namespace AtomSSH.Session.Connections;

internal interface ISshNetConnectionInfoFactory
{
    OperationResult<ConnectionInfo> Create(SshProfile profile, ConnectionRoute route, CredentialLease credentialLease);

    OperationResult<ConnectionInfo> Create(SshProfile profile, SshEndpoint endpoint, CredentialLease credentialLease);
}
