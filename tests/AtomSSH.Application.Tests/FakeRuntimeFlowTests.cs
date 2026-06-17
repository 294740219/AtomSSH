using AtomSSH.Application.DependencyInjection;
using AtomSSH.Application.Profiles;
using AtomSSH.Application.Sessions;
using AtomSSH.Application.Sftp;
using AtomSSH.Application.Transfers;
using AtomSSH.Core.Credentials;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Infrastructure.Configuration;
using AtomSSH.Infrastructure.DependencyInjection;
using AtomSSH.Network.DependencyInjection;
using AtomSSH.Session.DependencyInjection;
using AtomSSH.Transfer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AtomSSH.Application.Tests;

public sealed class FakeRuntimeFlowTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "AtomSSH.Flow.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ApplicationCanUseFakeRuntimeEndToEnd()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AtomSshDataDirectory(_tempDirectory));
        services.AddAtomSSHInfrastructure();
        services.AddAtomSSHNetwork();
        services.AddAtomSSHFakeSession();
        services.AddAtomSSHFakeTransfer();
        services.AddAtomSSHApplication();

        using var provider = services.BuildServiceProvider();
        var profileApp = provider.GetRequiredService<ProfileAppService>();
        var sessionApp = provider.GetRequiredService<SessionAppService>();
        var sftpApp = provider.GetRequiredService<SftpAppService>();
        var transferApp = provider.GetRequiredService<TransferAppService>();
        var profile = CreateProfile();

        var saveProfile = await profileApp.SaveAsync(profile, CancellationToken.None);
        var openTerminal = await sessionApp.OpenTerminalAsync(profile.Id, CancellationToken.None);
        var sftpList = await sftpApp.ListAsync(profile.Id, new RemotePath("/"), CancellationToken.None);
        var task = new SftpTransferTask(
            TransferTaskId.New(),
            profile.Id,
            TransferDirection.Download,
            new LocalPath("C:\\temp\\app.log"),
            new RemotePath("/app.log"),
            TransferOverwritePolicy.Overwrite,
            DateTimeOffset.UtcNow,
            TransferStatus.Pending);
        var createTransfer = await transferApp.CreateSftpTransferAsync(task, CancellationToken.None);
        var transferState = await transferApp.ListStateAsync(CancellationToken.None);

        Assert.True(saveProfile.Succeeded);
        Assert.True(openTerminal.Succeeded);
        Assert.True(sftpList.Succeeded);
        Assert.NotEmpty(sftpList.Value!);
        Assert.True(createTransfer.Succeeded);
        Assert.Contains(transferState.Value!, progress =>
            progress.TaskId == task.Id && progress.Status == TransferStatus.Succeeded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static SshProfile CreateProfile()
    {
        return new SshProfile(
            SshProfileId.New(),
            "fake-prod",
            new SshEndpoint(new HostName("fake.internal"), 22),
            "ops",
            SshAuthMethod.Password,
            CredentialRef.New(),
            Group: null,
            TerminalProfile: null);
    }
}
