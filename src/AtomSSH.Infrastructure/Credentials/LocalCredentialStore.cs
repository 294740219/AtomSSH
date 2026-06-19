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
        return await _metadataStore.UpdateAsync(
            new List<CredentialMetadata>(),
            list =>
            {
                list.RemoveAll(item => item.Ref == metadata.Ref);
                list.Add(metadata);
                return OperationResult<List<CredentialMetadata>>.Success(list);
            },
            cancellationToken).ConfigureAwait(false);
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

        var payload = ToSecretPayload(material);
        if (!payload.Succeeded || payload.Value is null)
        {
            return OperationResult.Failure(payload.Error!);
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload.Value);
        var protectedData = _protector.Protect(plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        if (!protectedData.Succeeded || protectedData.Value is null)
        {
            return OperationResult.Failure(protectedData.Error!);
        }

        var protectedPayload = Convert.ToBase64String(protectedData.Value);
        var saveSecret = await _secretStore.UpdateAsync(
            new List<EncryptedCredentialSecret>(),
            secrets =>
            {
                secrets.RemoveAll(item => item.Ref == metadata.Ref);
                secrets.Add(new EncryptedCredentialSecret(
                    metadata.Ref,
                    metadata.Kind,
                    protectedPayload,
                    DateTimeOffset.UtcNow));
                return OperationResult<List<EncryptedCredentialSecret>>.Success(secrets);
            },
            cancellationToken).ConfigureAwait(false);
        if (!saveSecret.Succeeded)
        {
            return saveSecret;
        }

        return await SaveAsync(metadata, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> DeleteAsync(CredentialRef credentialRef, CancellationToken cancellationToken)
    {
        var saveMetadata = await _metadataStore.UpdateAsync(
            new List<CredentialMetadata>(),
            list =>
            {
                list.RemoveAll(item => item.Ref == credentialRef);
                return OperationResult<List<CredentialMetadata>>.Success(list);
            },
            cancellationToken).ConfigureAwait(false);
        if (!saveMetadata.Succeeded)
        {
            return saveMetadata;
        }

        return await _secretStore.UpdateAsync(
            new List<EncryptedCredentialSecret>(),
            secrets =>
            {
                secrets.RemoveAll(item => item.Ref == credentialRef);
                return OperationResult<List<EncryptedCredentialSecret>>.Success(secrets);
            },
            cancellationToken).ConfigureAwait(false);
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

    private static OperationResult<CredentialSecretPayload> ToSecretPayload(CredentialMaterial material)
    {
        return material switch
        {
            PasswordCredentialMaterial password => OperationResult<CredentialSecretPayload>.Success(new CredentialSecretPayload(
                CredentialKind.Password,
                password.Password,
                null,
                null)),
            PrivateKeyCredentialMaterial privateKey => OperationResult<CredentialSecretPayload>.Success(new CredentialSecretPayload(
                privateKey.Kind,
                null,
                privateKey.PrivateKeyPem,
                privateKey.Passphrase)),
            KeyboardInteractiveCredentialMaterial keyboard => OperationResult<CredentialSecretPayload>.Success(new CredentialSecretPayload(
                CredentialKind.KeyboardInteractive,
                null,
                null,
                null,
                keyboard.Responses.ToDictionary(pair => pair.Key, pair => pair.Value),
                keyboard.DefaultResponse)),
            AgentCredentialMaterial => OperationResult<CredentialSecretPayload>.Success(new CredentialSecretPayload(
                CredentialKind.Agent,
                null,
                null,
                null,
                null,
                null)),
            _ => OperationResult<CredentialSecretPayload>.Failure(new SshError(
                SshErrorKind.Validation,
                "Unsupported credential material.",
                material.Kind.ToString()))
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
                OperationResult<CredentialMaterial>.Success(new KeyboardInteractiveCredentialMaterial(
                    payload.KeyboardInteractiveResponses ?? new Dictionary<string, string>(),
                    payload.KeyboardInteractiveDefaultResponse)),
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
    string? Passphrase,
    Dictionary<string, string>? KeyboardInteractiveResponses = null,
    string? KeyboardInteractiveDefaultResponse = null);
