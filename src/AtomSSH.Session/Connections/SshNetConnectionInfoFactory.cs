using System.Text;
using AtomSSH.Core.Credentials;
using AtomSSH.Core.Network;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using Renci.SshNet;

namespace AtomSSH.Session.Connections;

internal sealed class SshNetConnectionInfoFactory : ISshNetConnectionInfoFactory
{
    public OperationResult<ConnectionInfo> Create(SshProfile profile, ConnectionRoute route, CredentialLease credentialLease)
    {
        return Create(profile, route.Target, credentialLease);
    }

    public OperationResult<ConnectionInfo> Create(
        SshProfile profile,
        SshEndpoint endpoint,
        CredentialLease credentialLease)
    {
        var authenticationMethod = CreateAuthenticationMethod(profile, credentialLease.Material);
        if (!authenticationMethod.Succeeded || authenticationMethod.Value is null)
        {
            return OperationResult<ConnectionInfo>.Failure(authenticationMethod.Error!);
        }

        var connectionInfo = new ConnectionInfo(
            endpoint.Host.Value,
            endpoint.Port,
            profile.UserName,
            authenticationMethod.Value);

        return OperationResult<ConnectionInfo>.Success(connectionInfo);
    }

    private static OperationResult<AuthenticationMethod> CreateAuthenticationMethod(
        SshProfile profile,
        CredentialMaterial material)
    {
        var compatibility = ValidateCredentialCompatibility(profile.AuthMethod, material);
        if (!compatibility.Succeeded)
        {
            return OperationResult<AuthenticationMethod>.Failure(compatibility.Error!);
        }

        return material switch
        {
            PasswordCredentialMaterial password => OperationResult<AuthenticationMethod>.Success(
                new PasswordAuthenticationMethod(profile.UserName, password.Password)),
            PrivateKeyCredentialMaterial privateKey => CreatePrivateKeyAuthentication(profile, privateKey),
            KeyboardInteractiveCredentialMaterial => OperationResult<AuthenticationMethod>.Failure(new SshError(
                SshErrorKind.Validation,
                "Keyboard-interactive SSH authentication requires prompt response support and is not enabled yet.")),
            AgentCredentialMaterial => OperationResult<AuthenticationMethod>.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH agent authentication requires agent integration and is not enabled yet.")),
            _ => OperationResult<AuthenticationMethod>.Failure(new SshError(
                SshErrorKind.Validation,
                "Unsupported SSH credential material."))
        };
    }

    private static OperationResult ValidateCredentialCompatibility(
        SshAuthMethod authMethod,
        CredentialMaterial material)
    {
        return (authMethod, material) switch
        {
            (SshAuthMethod.Password, PasswordCredentialMaterial) => OperationResult.Success(),
            (SshAuthMethod.PrivateKey, PrivateKeyCredentialMaterial privateKey)
                when string.IsNullOrEmpty(privateKey.Passphrase) => OperationResult.Success(),
            (SshAuthMethod.PrivateKeyWithPassphrase, PrivateKeyCredentialMaterial privateKey)
                when !string.IsNullOrEmpty(privateKey.Passphrase) => OperationResult.Success(),
            (SshAuthMethod.KeyboardInteractive, KeyboardInteractiveCredentialMaterial) => OperationResult.Success(),
            (SshAuthMethod.Agent, AgentCredentialMaterial) => OperationResult.Success(),
            _ => OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "SSH credential material does not match the profile authentication method.",
                $"authMethod={authMethod}; material={material.Kind}"))
        };
    }

    private static OperationResult<AuthenticationMethod> CreatePrivateKeyAuthentication(
        SshProfile profile,
        PrivateKeyCredentialMaterial material)
    {
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(material.PrivateKeyPem));
            var privateKeyFile = string.IsNullOrEmpty(material.Passphrase)
                ? new PrivateKeyFile(stream)
                : new PrivateKeyFile(stream, material.Passphrase);

            return OperationResult<AuthenticationMethod>.Success(
                new PrivateKeyAuthenticationMethod(profile.UserName, privateKeyFile));
        }
        catch (Exception exception)
        {
            return OperationResult<AuthenticationMethod>.Failure(SshErrorRedactor.Redact(new SshError(
                SshErrorKind.Authentication,
                "SSH private key could not be loaded.",
                exception.Message)));
        }
    }
}
