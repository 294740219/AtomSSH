using System.Text.Json;
using AtomSSH.Core.CommandSnippets;
using AtomSSH.Core.ImportExport;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Settings;

namespace AtomSSH.Infrastructure.ImportExport;

public sealed class LocalImportExportService : IImportExportService
{
    private const string PackageVersion = "1";
    private const string ProfilesSection = "profiles";
    private const string SettingsSection = "settings";
    private const string NetworkInventorySection = "network-inventory";
    private const string CommandSnippetsSection = "command-snippets";
    private const string PortForwardProfilesSection = "port-forward-profiles";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly ISshProfileRepository _profiles;
    private readonly IApplicationSettingsRepository _settings;
    private readonly INetworkInventoryStore _networkInventory;
    private readonly ICommandSnippetRepository _snippets;
    private readonly IPortForwardProfileRepository _portForwards;

    public LocalImportExportService(
        ISshProfileRepository profiles,
        IApplicationSettingsRepository settings,
        INetworkInventoryStore networkInventory,
        ICommandSnippetRepository snippets,
        IPortForwardProfileRepository portForwards)
    {
        _profiles = profiles;
        _settings = settings;
        _networkInventory = networkInventory;
        _snippets = snippets;
        _portForwards = portForwards;
    }

    public async Task<OperationResult<ImportExportPackage>> ExportAsync(CancellationToken cancellationToken)
    {
        var profiles = await _profiles.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!profiles.Succeeded)
        {
            return OperationResult<ImportExportPackage>.Failure(profiles.Error!);
        }

        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Succeeded)
        {
            return OperationResult<ImportExportPackage>.Failure(settings.Error!);
        }

        var inventory = await ExportNetworkInventoryAsync(cancellationToken).ConfigureAwait(false);
        if (!inventory.Succeeded)
        {
            return OperationResult<ImportExportPackage>.Failure(inventory.Error!);
        }

        var snippets = await _snippets.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!snippets.Succeeded)
        {
            return OperationResult<ImportExportPackage>.Failure(snippets.Error!);
        }

        var portForwards = await _portForwards.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!portForwards.Succeeded)
        {
            return OperationResult<ImportExportPackage>.Failure(portForwards.Error!);
        }

        var package = new ImportExportPackage(
            PackageVersion,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                [ProfilesSection] = Serialize(profiles.Value!),
                [SettingsSection] = Serialize(settings.Value!),
                [NetworkInventorySection] = Serialize(inventory.Value!),
                [CommandSnippetsSection] = Serialize(snippets.Value!),
                [PortForwardProfilesSection] = Serialize(portForwards.Value!)
            });

        return OperationResult<ImportExportPackage>.Success(package);
    }

    public async Task<OperationResult<IReadOnlyList<ImportConflict>>> PreviewImportAsync(
        ImportExportPackage package,
        CancellationToken cancellationToken)
    {
        if (package.Version != PackageVersion)
        {
            IReadOnlyList<ImportConflict> unsupported =
            [
                new ImportConflict("package", package.Version, ImportConflictKind.UnsupportedVersion)
            ];
            return OperationResult<IReadOnlyList<ImportConflict>>.Success(unsupported);
        }

        var conflicts = new List<ImportConflict>();

        var profiles = DeserializeSection<IReadOnlyList<SshProfile>>(package, ProfilesSection);
        if (!profiles.Succeeded)
        {
            return OperationResult<IReadOnlyList<ImportConflict>>.Failure(profiles.Error!);
        }

        if (profiles.Value is not null)
        {
            var existingProfiles = await _profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            if (!existingProfiles.Succeeded)
            {
                return OperationResult<IReadOnlyList<ImportConflict>>.Failure(existingProfiles.Error!);
            }

            var existingIds = existingProfiles.Value!.Select(profile => profile.Id).ToHashSet();
            conflicts.AddRange(profiles.Value
                .Where(profile => existingIds.Contains(profile.Id))
                .Select(profile => new ImportConflict(
                    ProfilesSection,
                    profile.Id.Value.ToString(),
                    ImportConflictKind.AlreadyExists)));
        }

        var inventory = DeserializeSection<NetworkInventoryExport>(package, NetworkInventorySection);
        if (!inventory.Succeeded)
        {
            return OperationResult<IReadOnlyList<ImportConflict>>.Failure(inventory.Error!);
        }

        if (inventory.Value is not null)
        {
            var existingSpaces = await _networkInventory.ListSpacesAsync(cancellationToken).ConfigureAwait(false);
            if (!existingSpaces.Succeeded)
            {
                return OperationResult<IReadOnlyList<ImportConflict>>.Failure(existingSpaces.Error!);
            }

            var existingSpaceIds = existingSpaces.Value!.Select(space => space.Id).ToHashSet();
            conflicts.AddRange(inventory.Value.Spaces
                .Where(space => existingSpaceIds.Contains(space.Id))
                .Select(space => new ImportConflict(
                    NetworkInventorySection,
                    space.Id.ToString(),
                    ImportConflictKind.AlreadyExists)));

            foreach (var space in existingSpaces.Value!)
            {
                var existingNodes = await _networkInventory.ListNodesAsync(space.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (!existingNodes.Succeeded)
                {
                    return OperationResult<IReadOnlyList<ImportConflict>>.Failure(existingNodes.Error!);
                }

                var existingNodeIds = existingNodes.Value!.Select(node => node.Id).ToHashSet();
                conflicts.AddRange(inventory.Value.Nodes
                    .Where(node => existingNodeIds.Contains(node.Id))
                    .Select(node => new ImportConflict(
                        NetworkInventorySection,
                        node.Id.Value.ToString(),
                        ImportConflictKind.AlreadyExists)));
            }
        }

        var snippets = DeserializeSection<IReadOnlyList<CommandSnippet>>(package, CommandSnippetsSection);
        if (!snippets.Succeeded)
        {
            return OperationResult<IReadOnlyList<ImportConflict>>.Failure(snippets.Error!);
        }

        if (snippets.Value is not null)
        {
            var existingSnippets = await _snippets.ListAsync(cancellationToken).ConfigureAwait(false);
            if (!existingSnippets.Succeeded)
            {
                return OperationResult<IReadOnlyList<ImportConflict>>.Failure(existingSnippets.Error!);
            }

            var existingIds = existingSnippets.Value!.Select(snippet => snippet.Id).ToHashSet();
            conflicts.AddRange(snippets.Value
                .Where(snippet => existingIds.Contains(snippet.Id))
                .Select(snippet => new ImportConflict(
                    CommandSnippetsSection,
                    snippet.Id.Value.ToString(),
                    ImportConflictKind.AlreadyExists)));
        }

        var portForwards = DeserializeSection<IReadOnlyList<PortForwardProfile>>(package, PortForwardProfilesSection);
        if (!portForwards.Succeeded)
        {
            return OperationResult<IReadOnlyList<ImportConflict>>.Failure(portForwards.Error!);
        }

        if (portForwards.Value is not null)
        {
            var existingPortForwards = await _portForwards.ListAsync(cancellationToken).ConfigureAwait(false);
            if (!existingPortForwards.Succeeded)
            {
                return OperationResult<IReadOnlyList<ImportConflict>>.Failure(existingPortForwards.Error!);
            }

            var existingIds = existingPortForwards.Value!.Select(profile => profile.Id).ToHashSet();
            conflicts.AddRange(portForwards.Value
                .Where(profile => existingIds.Contains(profile.Id))
                .Select(profile => new ImportConflict(
                    PortForwardProfilesSection,
                    profile.Id.ToString(),
                    ImportConflictKind.AlreadyExists)));
        }

        return OperationResult<IReadOnlyList<ImportConflict>>.Success(conflicts);
    }

    public async Task<OperationResult<ImportResult>> ImportAsync(
        ImportExportPackage package,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        var preview = await PreviewImportAsync(package, cancellationToken).ConfigureAwait(false);
        if (!preview.Succeeded)
        {
            return OperationResult<ImportResult>.Failure(preview.Error!);
        }

        var conflicts = preview.Value!;
        if (conflicts.Any(conflict => conflict.Kind == ImportConflictKind.UnsupportedVersion))
        {
            return OperationResult<ImportResult>.Success(new ImportResult(conflicts, 0));
        }

        if (conflicts.Count > 0 && options.ConflictResolution == ImportConflictResolution.FailOnConflict)
        {
            return OperationResult<ImportResult>.Success(new ImportResult(conflicts, 0));
        }

        var importedSections = 0;

        var profiles = DeserializeSection<IReadOnlyList<SshProfile>>(package, ProfilesSection);
        if (!profiles.Succeeded)
        {
            return OperationResult<ImportResult>.Failure(profiles.Error!);
        }

        if (profiles.Value is not null)
        {
            foreach (var profile in profiles.Value)
            {
                var save = await _profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
                if (!save.Succeeded)
                {
                    return OperationResult<ImportResult>.Failure(save.Error!);
                }
            }

            importedSections++;
        }

        var settings = DeserializeSection<ApplicationSettings>(package, SettingsSection);
        if (!settings.Succeeded)
        {
            return OperationResult<ImportResult>.Failure(settings.Error!);
        }

        if (settings.Value is not null)
        {
            var save = await _settings.SaveAsync(settings.Value, cancellationToken).ConfigureAwait(false);
            if (!save.Succeeded)
            {
                return OperationResult<ImportResult>.Failure(save.Error!);
            }

            importedSections++;
        }

        var inventory = DeserializeSection<NetworkInventoryExport>(package, NetworkInventorySection);
        if (!inventory.Succeeded)
        {
            return OperationResult<ImportResult>.Failure(inventory.Error!);
        }

        if (inventory.Value is not null)
        {
            foreach (var space in inventory.Value.Spaces)
            {
                var save = await _networkInventory.SaveSpaceAsync(space, cancellationToken).ConfigureAwait(false);
                if (!save.Succeeded)
                {
                    return OperationResult<ImportResult>.Failure(save.Error!);
                }
            }

            foreach (var node in inventory.Value.Nodes)
            {
                var save = await _networkInventory.SaveNodeAsync(node, cancellationToken).ConfigureAwait(false);
                if (!save.Succeeded)
                {
                    return OperationResult<ImportResult>.Failure(save.Error!);
                }
            }

            importedSections++;
        }

        var snippets = DeserializeSection<IReadOnlyList<CommandSnippet>>(package, CommandSnippetsSection);
        if (!snippets.Succeeded)
        {
            return OperationResult<ImportResult>.Failure(snippets.Error!);
        }

        if (snippets.Value is not null)
        {
            foreach (var snippet in snippets.Value)
            {
                var save = await _snippets.SaveAsync(snippet, cancellationToken).ConfigureAwait(false);
                if (!save.Succeeded)
                {
                    return OperationResult<ImportResult>.Failure(save.Error!);
                }
            }

            importedSections++;
        }

        var portForwards = DeserializeSection<IReadOnlyList<PortForwardProfile>>(package, PortForwardProfilesSection);
        if (!portForwards.Succeeded)
        {
            return OperationResult<ImportResult>.Failure(portForwards.Error!);
        }

        if (portForwards.Value is not null)
        {
            foreach (var profile in portForwards.Value)
            {
                var save = await _portForwards.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
                if (!save.Succeeded)
                {
                    return OperationResult<ImportResult>.Failure(save.Error!);
                }
            }

            importedSections++;
        }

        return OperationResult<ImportResult>.Success(new ImportResult(conflicts, importedSections));
    }

    private async Task<OperationResult<NetworkInventoryExport>> ExportNetworkInventoryAsync(CancellationToken cancellationToken)
    {
        var spaces = await _networkInventory.ListSpacesAsync(cancellationToken).ConfigureAwait(false);
        if (!spaces.Succeeded)
        {
            return OperationResult<NetworkInventoryExport>.Failure(spaces.Error!);
        }

        var nodes = new List<NetworkNode>();
        foreach (var space in spaces.Value!)
        {
            var spaceNodes = await _networkInventory.ListNodesAsync(space.Id, cancellationToken)
                .ConfigureAwait(false);
            if (!spaceNodes.Succeeded)
            {
                return OperationResult<NetworkInventoryExport>.Failure(spaceNodes.Error!);
            }

            nodes.AddRange(spaceNodes.Value!);
        }

        return OperationResult<NetworkInventoryExport>.Success(new NetworkInventoryExport(spaces.Value!, nodes));
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    private static OperationResult<T?> DeserializeSection<T>(ImportExportPackage package, string section)
    {
        if (!package.Sections.TryGetValue(section, out var json))
        {
            return OperationResult<T?>.Success(default);
        }

        try
        {
            return OperationResult<T?>.Success(JsonSerializer.Deserialize<T>(json, SerializerOptions));
        }
        catch (JsonException exception)
        {
            return OperationResult<T?>.Failure(new SshError(
                SshErrorKind.Configuration,
                "Import package section is invalid JSON.",
                $"{section}: {exception.Message}"));
        }
    }

    private sealed record NetworkInventoryExport(
        IReadOnlyList<NetworkSpace> Spaces,
        IReadOnlyList<NetworkNode> Nodes);
}
