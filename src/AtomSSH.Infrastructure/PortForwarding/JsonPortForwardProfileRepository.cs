using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Results;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.PortForwarding;

public sealed class JsonPortForwardProfileRepository : IPortForwardProfileRepository
{
    private readonly JsonFileStore<List<PortForwardProfile>> _store;

    public JsonPortForwardProfileRepository(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<List<PortForwardProfile>>(directory.PortForwardProfilesFile);
    }

    public async Task<OperationResult<IReadOnlyList<PortForwardProfile>>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await _store.ReadAsync(new List<PortForwardProfile>(), cancellationToken).ConfigureAwait(false);
        return !list.Succeeded
            ? OperationResult<IReadOnlyList<PortForwardProfile>>.Failure(list.Error!)
            : OperationResult<IReadOnlyList<PortForwardProfile>>.Success(list.Value!);
    }

    public async Task<OperationResult> SaveAsync(PortForwardProfile profile, CancellationToken cancellationToken)
    {
        var list = await _store.ReadAsync(new List<PortForwardProfile>(), cancellationToken).ConfigureAwait(false);
        if (!list.Succeeded)
        {
            return OperationResult.Failure(list.Error!);
        }

        list.Value!.RemoveAll(item => item.Id == profile.Id);
        list.Value.Add(profile);
        return await _store.WriteAsync(list.Value, cancellationToken).ConfigureAwait(false);
    }
}
