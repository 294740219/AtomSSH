using AtomSSH.Core.Results;

namespace AtomSSH.Infrastructure.Configuration;

public sealed class AtomSshDataDirectory
{
    public AtomSshDataDirectory(string rootPath)
    {
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public string ProfilesFile => Path.Combine(RootPath, "profiles.json");

    public string SettingsFile => Path.Combine(RootPath, "settings.json");

    public string KnownHostsFile => Path.Combine(RootPath, "known-hosts.json");

    public string TransferTasksFile => Path.Combine(RootPath, "transfer-tasks.json");

    public string TransferStateFile => Path.Combine(RootPath, "transfer-state.json");

    public string NetworkInventoryFile => Path.Combine(RootPath, "network-inventory.json");

    public string CommandSnippetsFile => Path.Combine(RootPath, "command-snippets.json");

    public string PortForwardProfilesFile => Path.Combine(RootPath, "port-forward-profiles.json");

    public string CredentialMetadataFile => Path.Combine(RootPath, "credential-metadata.json");

    public string CredentialSecretsFile => Path.Combine(RootPath, "credential-secrets.json");

    public string LogsDirectory => Path.Combine(RootPath, "logs");

    public OperationResult EnsureCreated()
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(LogsDirectory);
            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(new SshError(
                SshErrorKind.Configuration,
                "AtomSSH data directory could not be created.",
                exception.Message));
        }
    }

    public static AtomSshDataDirectory CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = string.IsNullOrWhiteSpace(appData)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".atomssh")
            : Path.Combine(appData, "AtomSSH");

        return new AtomSshDataDirectory(root);
    }
}
