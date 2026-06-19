using AtomSSH.Core.Hosts;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.HostKeys;

public sealed class JsonHostKeyTrustStore : IHostKeyTrustStore
{
    private readonly JsonFileStore<List<KnownHostEntry>> _store;

    public JsonHostKeyTrustStore(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<List<KnownHostEntry>>(directory.KnownHostsFile);
    }

    public async Task<OperationResult<KnownHostEntry?>> FindAsync(HostName host, int port, CancellationToken cancellationToken)
    {
        var entries = await ReadEntriesAsync(cancellationToken).ConfigureAwait(false);
        return !entries.Succeeded
            ? OperationResult<KnownHostEntry?>.Failure(entries.Error!)
            : OperationResult<KnownHostEntry?>.Success(entries.Value!.FirstOrDefault(entry => entry.Host == host && entry.Port == port));
    }

    public async Task<OperationResult> SaveAsync(KnownHostEntry entry, CancellationToken cancellationToken)
    {
        return await _store.UpdateAsync(
            new List<KnownHostEntry>(),
            list =>
            {
                list.RemoveAll(item => item.Host == entry.Host && item.Port == entry.Port);
                list.Add(entry);
                return OperationResult<List<KnownHostEntry>>.Success(list);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private Task<OperationResult<List<KnownHostEntry>>> ReadEntriesAsync(CancellationToken cancellationToken)
    {
        return _store.ReadAsync(new List<KnownHostEntry>(), cancellationToken);
    }
}
