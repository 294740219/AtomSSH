using System.Text.Json;
using AtomSSH.Core.Results;

namespace AtomSSH.Infrastructure.Storage;

internal sealed class JsonFileStore<T>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonFileStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<OperationResult<T>> ReadAsync(T defaultValue, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return OperationResult<T>.Success(defaultValue);
            }

            await using var stream = File.OpenRead(_filePath);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return OperationResult<T>.Success(value ?? defaultValue);
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

    public async Task<OperationResult> WriteAsync(T value, CancellationToken cancellationToken)
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
                await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken)
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
}
