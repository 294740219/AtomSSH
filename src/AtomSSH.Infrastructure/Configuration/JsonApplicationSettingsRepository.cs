using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Settings;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.Configuration;

public sealed class JsonApplicationSettingsRepository : IApplicationSettingsRepository
{
    private readonly JsonFileStore<ApplicationSettings> _store;

    public JsonApplicationSettingsRepository(AtomSshDataDirectory directory)
    {
        _store = new JsonFileStore<ApplicationSettings>(directory.SettingsFile);
    }

    public Task<OperationResult<ApplicationSettings>> GetAsync(CancellationToken cancellationToken)
    {
        return _store.ReadAsync(new ApplicationSettings("System", "utf-8", null), cancellationToken);
    }

    public Task<OperationResult> SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        return _store.WriteAsync(settings, cancellationToken);
    }
}
