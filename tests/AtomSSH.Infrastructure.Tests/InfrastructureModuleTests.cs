using AtomSSH.Infrastructure;
using AtomSSH.Core.CommandSnippets;
using AtomSSH.Core.Credentials;
using AtomSSH.Core.ImportExport;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Settings;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Infrastructure.CommandSnippets;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.Credentials;
using AtomSSH.Infrastructure.DependencyInjection;
using AtomSSH.Infrastructure.ImportExport;
using AtomSSH.Infrastructure.NetworkInventory;
using AtomSSH.Infrastructure.PortForwarding;
using AtomSSH.Infrastructure.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace AtomSSH.Infrastructure.Tests;

public sealed class InfrastructureModuleTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "AtomSSH.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddAtomSSHInfrastructureReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAtomSSHInfrastructure();

        Assert.Same(services, result);
        Assert.Equal("AtomSSH.Infrastructure", InfrastructureModule.Name);
    }

    [Fact]
    public void AddAtomSSHInfrastructureRegistersRepositoryPorts()
    {
        var services = new ServiceCollection();

        services.AddAtomSSHInfrastructure();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISshProfileRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IApplicationSettingsRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostKeyTrustStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITransferTaskStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITransferStateStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(INetworkInventoryStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICommandSnippetRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IPortForwardProfileRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICredentialStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICredentialResolver));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IImportExportService));
    }

    [Fact]
    public async Task JsonApplicationSettingsRepositoryPersistsSettings()
    {
        var directory = CreateDirectory();
        var repository = new JsonApplicationSettingsRepository(directory);
        var settings = new ApplicationSettings("Dark", "utf-8", directory.LogsDirectory);

        var save = await repository.SaveAsync(settings, CancellationToken.None);
        var load = await repository.GetAsync(CancellationToken.None);

        Assert.True(save.Succeeded);
        Assert.True(load.Succeeded);
        Assert.Equal(settings, load.Value);
    }

    [Fact]
    public async Task JsonSshProfileRepositoryPersistsProfiles()
    {
        var directory = CreateDirectory();
        var repository = new JsonSshProfileRepository(directory);
        var profile = new SshProfile(
            SshProfileId.New(),
            "prod",
            new SshEndpoint(new HostName("prod.internal"), 22),
            "ops",
            SshAuthMethod.Password,
            CredentialRef.New(),
            Group: null,
            TerminalProfile: null);

        var save = await repository.SaveAsync(profile, CancellationToken.None);
        var load = await repository.GetAsync(profile.Id, CancellationToken.None);
        var list = await repository.ListAsync(CancellationToken.None);

        Assert.True(save.Succeeded);
        Assert.True(load.Succeeded);
        Assert.Equal(profile, load.Value);
        Assert.Single(list.Value!);
    }

    [Fact]
    public async Task LocalCredentialStorePersistsAndResolvesPasswordWithoutPlaintext()
    {
        var directory = CreateDirectory();
        var store = new LocalCredentialStore(directory);
        var credentialRef = CredentialRef.New();
        var metadata = new CredentialMetadata(credentialRef, CredentialKind.Password, "prod");

        var save = await store.SaveAsync(
            metadata,
            new PasswordCredentialMaterial("dont-write-me"),
            CancellationToken.None);
        var resolve = await store.ResolveAsync(credentialRef, CancellationToken.None);
        var metadataJson = await File.ReadAllTextAsync(directory.CredentialMetadataFile);
        var secretJson = await File.ReadAllTextAsync(directory.CredentialSecretsFile);

        Assert.True(save.Succeeded);
        Assert.True(resolve.Succeeded);
        var material = Assert.IsType<PasswordCredentialMaterial>(resolve.Value!.Material);
        Assert.Equal("dont-write-me", material.Password);
        Assert.DoesNotContain("dont-write-me", metadataJson);
        Assert.DoesNotContain("dont-write-me", secretJson);
    }

    [Fact]
    public async Task LocalCredentialStorePersistsAndResolvesPrivateKeyWithoutPlaintext()
    {
        var directory = CreateDirectory();
        var store = new LocalCredentialStore(directory);
        var credentialRef = CredentialRef.New();
        var metadata = new CredentialMetadata(credentialRef, CredentialKind.PrivateKeyWithPassphrase, "prod-key");
        const string privateKey = "-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----";

        var save = await store.SaveAsync(
            metadata,
            new PrivateKeyCredentialMaterial(privateKey, "key-passphrase"),
            CancellationToken.None);
        var resolve = await store.ResolveAsync(credentialRef, CancellationToken.None);
        var metadataJson = await File.ReadAllTextAsync(directory.CredentialMetadataFile);
        var secretJson = await File.ReadAllTextAsync(directory.CredentialSecretsFile);

        Assert.True(save.Succeeded);
        Assert.True(resolve.Succeeded);
        var material = Assert.IsType<PrivateKeyCredentialMaterial>(resolve.Value!.Material);
        Assert.Equal(privateKey, material.PrivateKeyPem);
        Assert.Equal("key-passphrase", material.Passphrase);
        Assert.DoesNotContain("abc", metadataJson);
        Assert.DoesNotContain("key-passphrase", metadataJson);
        Assert.DoesNotContain("abc", secretJson);
        Assert.DoesNotContain("key-passphrase", secretJson);
    }

    [Fact]
    public async Task JsonNetworkInventoryStorePersistsSpacesAndNodes()
    {
        var directory = CreateDirectory();
        var store = new JsonNetworkInventoryStore(directory);
        var space = new NetworkSpace(Guid.NewGuid(), "prod-vpc");
        var node = new NetworkNode(
            NetworkNodeId.New(),
            space.Id,
            "app-1",
            new NetworkAddress("10.0.1.12", 22),
            SshProfileId.New(),
            NetworkNodeRole.Target);

        var saveSpace = await store.SaveSpaceAsync(space, CancellationToken.None);
        var saveNode = await store.SaveNodeAsync(node, CancellationToken.None);
        var spaces = await store.ListSpacesAsync(CancellationToken.None);
        var nodes = await store.ListNodesAsync(space.Id, CancellationToken.None);

        Assert.True(saveSpace.Succeeded);
        Assert.True(saveNode.Succeeded);
        Assert.Contains(space, spaces.Value!);
        Assert.Contains(node, nodes.Value!);
    }

    [Fact]
    public async Task LocalImportExportServiceExportsConfigurationWithoutCredentialSecrets()
    {
        var directory = CreateDirectory();
        var profileRepository = new JsonSshProfileRepository(directory);
        var settingsRepository = new JsonApplicationSettingsRepository(directory);
        var inventoryStore = new JsonNetworkInventoryStore(directory);
        var snippetRepository = new JsonCommandSnippetRepository(directory);
        var portForwardRepository = new JsonPortForwardProfileRepository(directory);
        var credentialStore = new LocalCredentialStore(directory);
        var service = new LocalImportExportService(
            profileRepository,
            settingsRepository,
            inventoryStore,
            snippetRepository,
            portForwardRepository);
        var credentialRef = CredentialRef.New();
        var profile = CreateProfile(credentialRef);
        var space = new NetworkSpace(Guid.NewGuid(), "prod");
        var node = new NetworkNode(
            NetworkNodeId.New(),
            space.Id,
            "app",
            new NetworkAddress("10.0.0.10"),
            profile.Id,
            NetworkNodeRole.Target);
        var snippet = new CommandSnippet(CommandSnippetId.New(), "uptime", "uptime");
        var forward = new PortForwardProfile(
            Guid.NewGuid(),
            "postgres",
            profile.Id,
            PortForwardKind.Local,
            new PortForwardEndpoint("127.0.0.1", 15432),
            new PortForwardEndpoint("127.0.0.1", 5432));

        await profileRepository.SaveAsync(profile, CancellationToken.None);
        await settingsRepository.SaveAsync(new ApplicationSettings("Dark", "utf-8", directory.LogsDirectory), CancellationToken.None);
        await inventoryStore.SaveSpaceAsync(space, CancellationToken.None);
        await inventoryStore.SaveNodeAsync(node, CancellationToken.None);
        await snippetRepository.SaveAsync(snippet, CancellationToken.None);
        await portForwardRepository.SaveAsync(forward, CancellationToken.None);
        await credentialStore.SaveAsync(
            new CredentialMetadata(credentialRef, CredentialKind.Password, "prod"),
            new PasswordCredentialMaterial("dont-export-me"),
            CancellationToken.None);

        var export = await service.ExportAsync(CancellationToken.None);
        var packageText = string.Join(Environment.NewLine, export.Value!.Sections.Values);

        Assert.True(export.Succeeded);
        Assert.Contains("profiles", export.Value.Sections.Keys);
        Assert.Contains("network-inventory", export.Value.Sections.Keys);
        Assert.Contains("command-snippets", export.Value.Sections.Keys);
        Assert.Contains("port-forward-profiles", export.Value.Sections.Keys);
        Assert.Contains(profile.Id.Value.ToString(), packageText);
        Assert.DoesNotContain("dont-export-me", packageText);
    }

    [Fact]
    public async Task LocalImportExportServicePreviewReportsExistingProfileConflict()
    {
        var directory = CreateDirectory();
        var profileRepository = new JsonSshProfileRepository(directory);
        var service = new LocalImportExportService(
            profileRepository,
            new JsonApplicationSettingsRepository(directory),
            new JsonNetworkInventoryStore(directory),
            new JsonCommandSnippetRepository(directory),
            new JsonPortForwardProfileRepository(directory));
        var profile = CreateProfile(CredentialRef.New());

        await profileRepository.SaveAsync(profile, CancellationToken.None);
        var package = await service.ExportAsync(CancellationToken.None);
        var preview = await service.PreviewImportAsync(package.Value!, CancellationToken.None);

        Assert.True(preview.Succeeded);
        Assert.Contains(preview.Value!, conflict =>
            conflict.Section == "profiles"
            && conflict.Key == profile.Id.Value.ToString()
            && conflict.Kind == ImportConflictKind.AlreadyExists);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private AtomSshDataDirectory CreateDirectory()
    {
        var directory = new AtomSshDataDirectory(_tempDirectory);
        var result = directory.EnsureCreated();
        Assert.True(result.Succeeded);
        return directory;
    }

    private static SshProfile CreateProfile(CredentialRef credentialRef)
    {
        return new SshProfile(
            SshProfileId.New(),
            "prod",
            new SshEndpoint(new HostName("prod.internal"), 22),
            "ops",
            SshAuthMethod.Password,
            credentialRef,
            Group: null,
            TerminalProfile: null);
    }
}
