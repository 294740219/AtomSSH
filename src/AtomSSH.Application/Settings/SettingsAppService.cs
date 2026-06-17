using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.Settings;

namespace AtomSSH.Application.Settings;

public sealed class SettingsAppService
{
    private readonly IApplicationSettingsRepository _settings;

    public SettingsAppService(IApplicationSettingsRepository settings)
    {
        _settings = settings;
    }

    public Task<OperationResult<ApplicationSettings>> GetAsync(CancellationToken cancellationToken)
    {
        return _settings.GetAsync(cancellationToken);
    }

    public Task<OperationResult> SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        return _settings.SaveAsync(settings, cancellationToken);
    }
}
