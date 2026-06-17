using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.Profiles;

public sealed class JsonSshProfileRepository : ISshProfileRepository
{
    private readonly JsonFileStore<List<SshProfile>> _store;

    public JsonSshProfileRepository(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<List<SshProfile>>(directory.ProfilesFile);
    }

    public async Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken)
    {
        var list = await ReadProfilesAsync(cancellationToken).ConfigureAwait(false);
        return !list.Succeeded
            ? OperationResult<SshProfile?>.Failure(list.Error!)
            : OperationResult<SshProfile?>.Success(list.Value!.FirstOrDefault(profile => profile.Id == id));
    }

    public async Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await ReadProfilesAsync(cancellationToken).ConfigureAwait(false);
        return !list.Succeeded
            ? OperationResult<IReadOnlyList<SshProfile>>.Failure(list.Error!)
            : OperationResult<IReadOnlyList<SshProfile>>.Success(list.Value!);
    }

    public async Task<OperationResult> SaveAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        var list = await ReadProfilesAsync(cancellationToken).ConfigureAwait(false);
        if (!list.Succeeded)
        {
            return OperationResult.Failure(list.Error!);
        }

        var profiles = list.Value!;
        var existingIndex = profiles.FindIndex(item => item.Id == profile.Id);
        if (existingIndex >= 0)
        {
            profiles[existingIndex] = profile;
        }
        else
        {
            profiles.Add(profile);
        }

        return await _store.WriteAsync(profiles, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteAsync(SshProfileId id, CancellationToken cancellationToken)
    {
        var list = await ReadProfilesAsync(cancellationToken).ConfigureAwait(false);
        if (!list.Succeeded)
        {
            return OperationResult.Failure(list.Error!);
        }

        list.Value!.RemoveAll(profile => profile.Id == id);
        return await _store.WriteAsync(list.Value!, cancellationToken).ConfigureAwait(false);
    }

    private Task<OperationResult<List<SshProfile>>> ReadProfilesAsync(CancellationToken cancellationToken)
    {
        return _store.ReadAsync(new List<SshProfile>(), cancellationToken);
    }
}
