using System.Text.Json;
using System.Security.Cryptography;
using AtomSSH.Core.Credentials;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Storage;

namespace AtomSSH.Infrastructure.Credentials;

public sealed class LocalCredentialStore : ICredentialStore, ICredentialResolver
{
    private readonly JsonFileStore<List<CredentialMetadata>> _metadataStore;
    private readonly JsonFileStore<List<EncryptedCredentialSecret>> _secretStore;
    private readonly WindowsDpapiSecretProtector _protector = new();

    public LocalCredentialStore(AtomSshDataDirectory directory)
    {
        _metadataStore = new JsonFileStore<List<CredentialMetadata>>(directory.CredentialMetadataFile);
        _secretStore = new JsonFileStore<List<EncryptedCredentialSecret>>(directory.CredentialSecretsFile);
    }

    public async Task<OperationResult> SaveAsync(CredentialMetadata metadata, CancellationToken cancellationToken)
    {
        var list = await _metadataStore.ReadAsync(new List<CredentialMetadata>(), cancellationToken).ConfigureAwait(false);
        if (!list.Succeeded)
        {
            return OperationResult.Failure(list.Error!);
        }

        list.Value!.RemoveAll(item => item.Ref == metadata.Ref);
        list.Value.Add(metadata);
        return await _metadataStore.WriteAsync(list.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> SaveAsync(
        CredentialMetadata metadata,
        CredentialMaterial material,
        CancellationToken cancellationToken)
    {
        if (metadata.Kind != material.Kind)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Validation,
                "Credential metadata kind does not match credential material kind.",
                $"metadata={metadata.Kind}; material={material.Kind}"));
        }

        var saveMetadata = await SaveAsync(metadata, cancellationToken).ConfigureAwait(false);
        if (!saveMetadata.Succeeded)
        {
            return saveMetadata;
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(ToSecretPayload(material));
        var protectedData = _protector.Protect(plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        if (!protectedData.Succeeded || protectedData.Value is null)
        {
            return OperationResult.Failure(protectedData.Error!);
        }

        var secrets = await _secretStore.ReadAsync(new List<EncryptedCredentialSecret>(), cancellationToken)
            .ConfigureAwait(false);
        if (!secrets.Succeeded)
        {
            return OperationResult.Failure(secrets.Error!);
        }

        secrets.Value!.RemoveAll(item => item.Ref == metadata.Ref);
        secrets.Value.Add(new EncryptedCredentialSecret(
            metadata.Ref,
            metadata.Kind,
            Convert.ToBase64String(protectedData.Value),
            DateTimeOffset.UtcNow));

        return await _secretStore.WriteAsync(secrets.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteAsync(CredentialRef credentialRef, CancellationToken cancellationToken)
    {
        var list = await _metadataStore.ReadAsync(new List<CredentialMetadata>(), cancellationToken).ConfigureAwait(false);
        if (!list.Succeeded)
        {
            return OperationResult.Failure(list.Error!);
        }

        list.Value!.RemoveAll(item => item.Ref == credentialRef);
        var saveMetadata = await _metadataStore.WriteAsync(list.Value, cancellationToken).ConfigureAwait(false);
        if (!saveMetadata.Succeeded)
        {
            return saveMetadata;
        }

        var secrets = await _secretStore.ReadAsync(new List<EncryptedCredentialSecret>(), cancellationToken)
            .ConfigureAwait(false);
        if (!secrets.Succeeded)
        {
            return OperationResult.Failure(secrets.Error!);
        }

        secrets.Value!.RemoveAll(item => item.Ref == credentialRef);
        return await _secretStore.WriteAsync(secrets.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult<CredentialLease>> ResolveAsync(
        CredentialRef credentialRef,
        CancellationToken cancellationToken)
    {
        var secrets = await _secretStore.ReadAsync(new List<EncryptedCredentialSecret>(), cancellationToken)
            .ConfigureAwait(false);
        if (!secrets.Succeeded)
        {
            return OperationResult<CredentialLease>.Failure(secrets.Error!);
        }

        var secret = secrets.Value!.FirstOrDefault(item => item.Ref == credentialRef);
        if (secret is null)
        {
            return OperationResult<CredentialLease>.Failure(new SshError(
                SshErrorKind.Configuration,
                "Credential secret was not found.",
                credentialRef.Value.ToString()));
        }

        byte[] protectedData;
        try
        {
            protectedData = Convert.FromBase64String(secret.ProtectedPayload);
        }
        catch (FormatException exception)
        {
            return OperationResult<CredentialLease>.Failure(new SshError(
                SshErrorKind.Configuration,
                "Credential secret payload is invalid.",
                exception.Message));
        }

        var plaintext = _protector.Unprotect(protectedData);
        if (!plaintext.Succeeded || plaintext.Value is null)
        {
            return OperationResult<CredentialLease>.Failure(plaintext.Error!);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<CredentialSecretPayload>(plaintext.Value);
            if (payload is null)
            {
                return OperationResult<CredentialLease>.Failure(new SshError(
                    SshErrorKind.Configuration,
                    "Credential secret payload is empty."));
            }

            var material = FromSecretPayload(payload);
            if (!material.Succeeded || material.Value is null)
            {
                return OperationResult<CredentialLease>.Failure(material.Error!);
            }

            return OperationResult<CredentialLease>.Success(new CredentialLease(
                credentialRef,
                material.Value,
                DateTimeOffset.UtcNow));
        }
        catch (JsonException exception)
        {
            return OperationResult<CredentialLease>.Failure(new SshError(
                SshErrorKind.Configuration,
                "Credential secret payload could not be read.",
                SshErrorRedactor.RedactDetail(exception.Message)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext.Value);
        }
    }

    private static CredentialSecretPayload ToSecretPayload(CredentialMaterial material)
    {
        return material switch
        {
            PasswordCredentialMaterial password => new CredentialSecretPayload(
                CredentialKind.Password,
                password.Password,
                null,
                null),
            PrivateKeyCredentialMaterial privateKey => new CredentialSecretPayload(
                privateKey.Kind,
                null,
                privateKey.PrivateKeyPem,
                privateKey.Passphrase),
            KeyboardInteractiveCredentialMaterial => new CredentialSecretPayload(
                CredentialKind.KeyboardInteractive,
                null,
                null,
                null),
            AgentCredentialMaterial => new CredentialSecretPayload(
                CredentialKind.Agent,
                null,
                null,
                null),
            _ => throw new InvalidOperationException("Unsupported credential material.")
        };
    }

    private static OperationResult<CredentialMaterial> FromSecretPayload(CredentialSecretPayload payload)
    {
        return payload.Kind switch
        {
            CredentialKind.Password when payload.Password is not null =>
                OperationResult<CredentialMaterial>.Success(new PasswordCredentialMaterial(payload.Password)),
            CredentialKind.PrivateKey when payload.PrivateKeyPem is not null =>
                OperationResult<CredentialMaterial>.Success(new PrivateKeyCredentialMaterial(payload.PrivateKeyPem, null)),
            CredentialKind.PrivateKeyWithPassphrase when payload.PrivateKeyPem is not null =>
                OperationResult<CredentialMaterial>.Success(new PrivateKeyCredentialMaterial(
                    payload.PrivateKeyPem,
                    payload.Passphrase)),
            CredentialKind.KeyboardInteractive =>
                OperationResult<CredentialMaterial>.Success(new KeyboardInteractiveCredentialMaterial()),
            CredentialKind.Agent =>
                OperationResult<CredentialMaterial>.Success(new AgentCredentialMaterial()),
            _ => OperationResult<CredentialMaterial>.Failure(new SshError(
                SshErrorKind.Configuration,
                "Credential secret payload does not match its credential kind.",
                payload.Kind.ToString()))
        };
    }
}

public sealed record EncryptedCredentialSecret(
    CredentialRef Ref,
    CredentialKind Kind,
    string ProtectedPayload,
    DateTimeOffset UpdatedAt);

public sealed record CredentialSecretPayload(
    CredentialKind Kind,
    string? Password,
    string? PrivateKeyPem,
    string? Passphrase);
