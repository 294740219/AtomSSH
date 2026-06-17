using Renci.SshNet;

namespace AtomSSH.Session.Connections;

internal sealed class SshNetClientConnection<TClient> : IDisposable
    where TClient : BaseClient
{
    private readonly List<IDisposable> _ownedResources;

    public SshNetClientConnection(TClient client, IEnumerable<IDisposable>? ownedResources = null)
    {
        Client = client;
        _ownedResources = ownedResources?.ToList() ?? [];
    }

    public TClient Client { get; }

    public void Dispose()
    {
        if (Client.IsConnected)
        {
            Client.Disconnect();
        }

        Client.Dispose();

        for (var index = _ownedResources.Count - 1; index >= 0; index--)
        {
            _ownedResources[index].Dispose();
        }
    }
}
