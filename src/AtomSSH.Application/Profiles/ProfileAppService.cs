using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Application.Profiles;

public sealed class ProfileAppService
{
    private readonly ISshProfileRepository _profiles;

    public ProfileAppService(ISshProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken)
    {
        return _profiles.ListAsync(cancellationToken);
    }

    public Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken)
    {
        return _profiles.GetAsync(id, cancellationToken);
    }

    public Task<OperationResult> SaveAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile name is required.")));
        }

        if (string.IsNullOrWhiteSpace(profile.Endpoint.Host.Value))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile host is required.")));
        }

        if (profile.Endpoint.Port <= 0 || profile.Endpoint.Port > 65535)
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile port must be between 1 and 65535.",
                profile.Endpoint.Port.ToString())));
        }

        if (string.IsNullOrWhiteSpace(profile.UserName))
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile user name is required.")));
        }

        if (profile.AuthMethod is SshAuthMethod.Password or SshAuthMethod.PrivateKey or SshAuthMethod.PrivateKeyWithPassphrase
            && profile.CredentialRef is null)
        {
            return Task.FromResult(OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH profile credential reference is required for the selected authentication method.")));
        }

        return _profiles.SaveAsync(profile, cancellationToken);
    }

    public Task<OperationResult> DeleteAsync(SshProfileId id, CancellationToken cancellationToken)
    {
        return _profiles.DeleteAsync(id, cancellationToken);
    }
}
