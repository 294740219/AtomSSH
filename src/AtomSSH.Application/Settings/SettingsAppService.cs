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
        if (string.IsNullOrWhiteSpace(settings.ThemeName))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Application theme name is required.")));
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultEncoding))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Application default encoding is required.")));
        }

        try
        {
            _ = System.Text.Encoding.GetEncoding(settings.DefaultEncoding);
        }
        catch (ArgumentException exception)
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Application default encoding is invalid.",
                exception.Message)));
        }

        return _settings.SaveAsync(settings, cancellationToken);
    }
}
