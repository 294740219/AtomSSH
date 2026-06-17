using AtomSSH.Core.Credentials;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Transfers;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Transfer;
using AtomSSH.Transfer.DependencyInjection;
using AtomSSH.Transfer.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace AtomSSH.Transfer.Tests;

public sealed class TransferModuleTests
{
    [Fact]
    public void AddAtomSSHTransferReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAtomSSHTransfer();

        Assert.Same(services, result);
        Assert.Equal("AtomSSH.Transfer", TransferModule.Name);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ITransferTaskScheduler)
            && descriptor.ImplementationType == typeof(SftpTransferTaskScheduler)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddAtomSSHRealTransferRegistersSftpScheduler()
    {
        var services = new ServiceCollection();

        services.AddAtomSSHRealTransfer();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ITransferTaskScheduler)
            && descriptor.ImplementationType == typeof(SftpTransferTaskScheduler)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddAtomSSHFakeTransferRegistersFakeScheduler()
    {
        var services = new ServiceCollection();

        services.AddAtomSSHFakeTransfer();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ITransferTaskScheduler)
            && descriptor.ImplementationType == typeof(FakeTransferTaskScheduler)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task SftpTransferSchedulerUploadsAndSavesProgress()
    {
        var profile = CreateProfile();
        var task = CreateTask(profile.Id, TransferDirection.Upload);
        var stateStore = new InMemoryTransferStateStore();
        var fileTransfer = new StubSftpFileTransfer(123);
        var scheduler = new SftpTransferTaskScheduler(
            new StubProfileRepository(profile),
            fileTransfer,
            stateStore);

        var result = await scheduler.SubmitAsync(task, CreatePlan(task.Id, profile.Endpoint), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(fileTransfer.UploadCalled);
        Assert.False(fileTransfer.DownloadCalled);
        Assert.Contains(stateStore.Progress, progress => progress.Status == TransferStatus.Running);
        Assert.Contains(stateStore.Progress, progress =>
            progress.Status == TransferStatus.Succeeded
            && progress.BytesTransferred == 123
            && progress.TotalBytes == 123);
    }

    [Fact]
    public async Task SftpTransferSchedulerDownloadsAndSavesProgress()
    {
        var profile = CreateProfile();
        var task = CreateTask(profile.Id, TransferDirection.Download);
        var stateStore = new InMemoryTransferStateStore();
        var fileTransfer = new StubSftpFileTransfer(456);
        var scheduler = new SftpTransferTaskScheduler(
            new StubProfileRepository(profile),
            fileTransfer,
            stateStore);

        var result = await scheduler.SubmitAsync(task, CreatePlan(task.Id, profile.Endpoint), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(fileTransfer.UploadCalled);
        Assert.True(fileTransfer.DownloadCalled);
        Assert.Contains(stateStore.Progress, progress =>
            progress.Status == TransferStatus.Succeeded
            && progress.BytesTransferred == 456
            && progress.TotalBytes == 456);
    }

    [Fact]
    public async Task SftpTransferSchedulerSavesCancelledWhenFileTransferIsCancelled()
    {
        var profile = CreateProfile();
        var task = CreateTask(profile.Id, TransferDirection.Upload);
        var stateStore = new InMemoryTransferStateStore();
        var fileTransfer = new StubSftpFileTransfer(0)
        {
            UploadException = new OperationCanceledException("password=cancelled")
        };
        var scheduler = new SftpTransferTaskScheduler(
            new StubProfileRepository(profile),
            fileTransfer,
            stateStore);

        var result = await scheduler.SubmitAsync(task, CreatePlan(task.Id, profile.Endpoint), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(stateStore.Progress, progress =>
            progress.Status == TransferStatus.Cancelled
            && progress.LastError?.Kind == SshErrorKind.Cancelled);
        Assert.DoesNotContain("cancelled", result.Error!.Detail);
    }

    [Fact]
    public async Task SftpTransferSchedulerSavesFailedWithRedactedErrorWhenFileTransferThrows()
    {
        var profile = CreateProfile();
        var task = CreateTask(profile.Id, TransferDirection.Upload);
        var stateStore = new InMemoryTransferStateStore();
        var fileTransfer = new StubSftpFileTransfer(0)
        {
            UploadException = new InvalidOperationException("token=abc")
        };
        var scheduler = new SftpTransferTaskScheduler(
            new StubProfileRepository(profile),
            fileTransfer,
            stateStore);

        var result = await scheduler.SubmitAsync(task, CreatePlan(task.Id, profile.Endpoint), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(stateStore.Progress, progress =>
            progress.Status == TransferStatus.Failed
            && progress.LastError?.Kind == SshErrorKind.Internal);
        Assert.DoesNotContain("abc", result.Error!.Detail);
    }

    [Fact]
    public async Task SftpTransferSchedulerCopiesRemoteToRemoteUsingLocalRelay()
    {
        var source = CreateProfile("source", "source.internal");
        var target = CreateProfile("target", "target.internal");
        var task = new RemoteCopyTask(
            TransferTaskId.New(),
            source.Id,
            target.Id,
            new RemotePath("/var/source.log"),
            new RemotePath("/tmp/source.log"),
            RemoteCopyMode.LocalRelay,
            TransferOverwritePolicy.Overwrite,
            DateTimeOffset.UtcNow,
            TransferStatus.Pending);
        var stateStore = new InMemoryTransferStateStore();
        var fileTransfer = new StubSftpFileTransfer(0)
        {
            ReadBytes = "relay-data"u8.ToArray()
        };
        var scheduler = new SftpTransferTaskScheduler(
            new StubProfileRepository(source, target),
            fileTransfer,
            stateStore);

        var result = await scheduler.SubmitAsync(
            task,
            new TransferExecutionPlan(
                task.Id,
                new ConnectionRoute(ConnectionRouteKind.Direct, source.Endpoint, Array.Empty<JumpHostRoute>()),
                new ConnectionRoute(ConnectionRouteKind.Direct, target.Endpoint, Array.Empty<JumpHostRoute>())),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(fileTransfer.ReadBytes, fileTransfer.WrittenBytes);
        Assert.Contains(stateStore.Progress, progress => progress.Status == TransferStatus.Running);
        Assert.Contains(stateStore.Progress, progress =>
            progress.Status == TransferStatus.Succeeded
            && progress.BytesTransferred == fileTransfer.ReadBytes.Length
            && progress.TotalBytes == fileTransfer.ReadBytes.Length);
    }

    private static SftpTransferTask CreateTask(SshProfileId profileId, TransferDirection direction)
    {
        return new SftpTransferTask(
            TransferTaskId.New(),
            profileId,
            direction,
            new LocalPath("C:\\temp\\atomssh.txt"),
            new RemotePath("/tmp/atomssh.txt"),
            TransferOverwritePolicy.Overwrite,
            DateTimeOffset.UtcNow,
            TransferStatus.Pending);
    }

    private static SshProfile CreateProfile()
    {
        return CreateProfile("example", "example.internal");
    }

    private static SshProfile CreateProfile(string name, string host)
    {
        return new SshProfile(
            SshProfileId.New(),
            name,
            new SshEndpoint(new HostName(host), 22),
            "ops",
            SshAuthMethod.Password,
            CredentialRef.New(),
            Group: null,
            TerminalProfile: null);
    }

    private static TransferExecutionPlan CreatePlan(TransferTaskId taskId, SshEndpoint endpoint)
    {
        return new TransferExecutionPlan(
            taskId,
            new ConnectionRoute(ConnectionRouteKind.Direct, endpoint, Array.Empty<JumpHostRoute>()));
    }

    private sealed class StubProfileRepository : ISshProfileRepository
    {
        private readonly Dictionary<SshProfileId, SshProfile> _profiles;

        public StubProfileRepository(params SshProfile[] profiles)
        {
            _profiles = profiles.ToDictionary(profile => profile.Id);
        }

        public Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken)
        {
            _profiles.TryGetValue(id, out var profile);
            return Task.FromResult(OperationResult<SshProfile?>.Success(profile));
        }

        public Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<SshProfile> profiles = _profiles.Values.ToArray();
            return Task.FromResult(OperationResult<IReadOnlyList<SshProfile>>.Success(profiles));
        }

        public Task<OperationResult> SaveAsync(SshProfile profile, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> DeleteAsync(SshProfileId id, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class StubSftpFileTransfer : ISftpFileTransfer
    {
        private readonly long _bytes;

        public StubSftpFileTransfer(long bytes)
        {
            _bytes = bytes;
        }

        public bool UploadCalled { get; private set; }

        public bool DownloadCalled { get; private set; }

        public byte[] ReadBytes { get; init; } = [];

        public byte[] WrittenBytes { get; private set; } = [];

        public Exception? UploadException { get; init; }

        public Exception? DownloadException { get; init; }

        public Task<OperationResult<long>> UploadAsync(
            SshProfile profile,
            ConnectionRoute route,
            LocalPath localPath,
            RemotePath remotePath,
            TransferOverwritePolicy overwritePolicy,
            CancellationToken cancellationToken)
        {
            if (UploadException is not null)
            {
                throw UploadException;
            }

            UploadCalled = true;
            return Task.FromResult(OperationResult<long>.Success(_bytes));
        }

        public Task<OperationResult<long>> DownloadAsync(
            SshProfile profile,
            ConnectionRoute route,
            RemotePath remotePath,
            LocalPath localPath,
            TransferOverwritePolicy overwritePolicy,
            CancellationToken cancellationToken)
        {
            if (DownloadException is not null)
            {
                throw DownloadException;
            }

            DownloadCalled = true;
            return Task.FromResult(OperationResult<long>.Success(_bytes));
        }

        public Task<OperationResult<ISftpFileStreamLease>> OpenReadAsync(
            SshProfile profile,
            ConnectionRoute route,
            RemotePath remotePath,
            CancellationToken cancellationToken)
        {
            ISftpFileStreamLease lease = new MemorySftpFileStreamLease(
                new MemoryStream(ReadBytes, writable: false),
                ReadBytes.Length,
                null);
            return Task.FromResult(OperationResult<ISftpFileStreamLease>.Success(lease));
        }

        public Task<OperationResult<ISftpFileStreamLease>> OpenWriteAsync(
            SshProfile profile,
            ConnectionRoute route,
            RemotePath remotePath,
            TransferOverwritePolicy overwritePolicy,
            CancellationToken cancellationToken)
        {
            var stream = new MemoryStream();
            ISftpFileStreamLease lease = new MemorySftpFileStreamLease(
                stream,
                null,
                bytes => WrittenBytes = bytes);
            return Task.FromResult(OperationResult<ISftpFileStreamLease>.Success(lease));
        }
    }

    private sealed class MemorySftpFileStreamLease : ISftpFileStreamLease
    {
        private readonly Action<byte[]>? _onDispose;

        public MemorySftpFileStreamLease(Stream stream, long? length, Action<byte[]>? onDispose)
        {
            Stream = stream;
            Length = length;
            _onDispose = onDispose;
        }

        public Stream Stream { get; }

        public long? Length { get; }

        public async ValueTask DisposeAsync()
        {
            if (_onDispose is not null && Stream is MemoryStream memoryStream)
            {
                _onDispose(memoryStream.ToArray());
            }

            await Stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class InMemoryTransferStateStore : ITransferStateStore
    {
        public List<TransferProgress> Progress { get; } = [];

        public Task<OperationResult<IReadOnlyList<TransferProgress>>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<IReadOnlyList<TransferProgress>>.Success(Progress));
        }

        public Task<OperationResult> SaveAsync(TransferProgress progress, CancellationToken cancellationToken)
        {
            Progress.Add(progress);
            return Task.FromResult(OperationResult.Success());
        }
    }
}
