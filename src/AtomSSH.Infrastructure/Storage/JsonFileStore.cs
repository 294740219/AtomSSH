using System.Text.Json;
using System.Collections.Concurrent;
using AtomSSH.Core.Results;

namespace AtomSSH.Infrastructure.Storage;

internal sealed class JsonFileStore<T>
{
    private const int CurrentSchemaVersion = 1;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate;

    public JsonFileStore(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
        _gate = Gates.GetOrAdd(_filePath, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<OperationResult<T>> ReadAsync(T defaultValue, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadUnlockedAsync(defaultValue, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperationResult> WriteAsync(T value, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await WriteUnlockedAsync(value, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperationResult> UpdateAsync(
        T defaultValue,
        Func<T, OperationResult<T>> update,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await ReadUnlockedAsync(defaultValue, cancellationToken).ConfigureAwait(false);
            if (!current.Succeeded)
            {
                return OperationResult.Failure(current.Error!);
            }

            var updated = update(current.Value!);
            if (!updated.Succeeded)
            {
                return OperationResult.Failure(updated.Error!);
            }

            return await WriteUnlockedAsync(updated.Value!, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<OperationResult<T>> ReadUnlockedAsync(T defaultValue, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return OperationResult<T>.Success(defaultValue);
            }

            await using var stream = File.OpenRead(_filePath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            JsonElement dataElement;
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && (document.RootElement.TryGetProperty("schemaVersion", out _)
                    || document.RootElement.TryGetProperty("SchemaVersion", out _))
                && (document.RootElement.TryGetProperty("data", out dataElement)
                    || document.RootElement.TryGetProperty("Data", out dataElement)))
            {
                var enveloped = dataElement.Deserialize<T>(SerializerOptions);
                return OperationResult<T>.Success(enveloped ?? defaultValue);
            }

            var legacy = document.RootElement.Deserialize<T>(SerializerOptions);
            return OperationResult<T>.Success(legacy ?? defaultValue);
        }
        catch (JsonException exception)
        {
            return OperationResult<T>.Failure(new SshError(
                SshErrorKind.Configuration,
                "AtomSSH configuration file is invalid JSON.",
                $"{_filePath}: {exception.Message}"));
        }
        catch (Exception exception)
        {
            return OperationResult<T>.Failure(new SshError(
                SshErrorKind.Configuration,
                "AtomSSH configuration file could not be read.",
                $"{_filePath}: {exception.Message}"));
        }
    }

    private async Task<OperationResult> WriteUnlockedAsync(T value, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        new JsonFileEnvelope<T>(CurrentSchemaVersion, value),
                        SerializerOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, _filePath, overwrite: true);
            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Configuration,
                "AtomSSH configuration file could not be written.",
                $"{_filePath}: {exception.Message}"));
        }
    }

    private sealed record JsonFileEnvelope<TData>(int SchemaVersion, TData Data);
}
