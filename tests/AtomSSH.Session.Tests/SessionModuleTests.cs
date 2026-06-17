using AtomSSH.Session;
using AtomSSH.Core.Credentials;
using AtomSSH.Core.Hosts;
using AtomSSH.Core.Network;
using AtomSSH.Core.Ports;
using AtomSSH.Core.PortForwarding;
using AtomSSH.Core.Profiles;
using AtomSSH.Core.Results;
using AtomSSH.Core.Terminal;
using AtomSSH.Core.ValueObjects;
using AtomSSH.Session.Connections;
using AtomSSH.Session.DependencyInjection;
using AtomSSH.Session.HostKeys;
using AtomSSH.Session.PortForwarding;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace AtomSSH.Session.Tests;

public sealed class SessionModuleTests
{
    [Fact]
    public void AddAtomSSHSessionReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAtomSSHSession();

        Assert.Same(services, result);
        Assert.Equal("AtomSSH.Session", SessionModule.Name);
    }

    [Fact]
    public void AddAtomSSHSessionRegistersRuntimePorts()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialResolver, StubCredentialResolver>();
        services.AddAtomSSHSession();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(SshNetSessionRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISshSessionRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISshSessionFactory)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISftpFileTransfer)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPortForwardRuntime)
            && descriptor.ImplementationType == typeof(SshNetPortForwardRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddAtomSSHFakeSessionRegistersFakeRuntimePorts()
    {
        var services = new ServiceCollection();

        services.AddAtomSSHFakeSession();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(FakeSshSessionRuntime)
            && descriptor.ImplementationType == typeof(FakeSshSessionRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISshSessionRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISshSessionFactory)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPortForwardRuntime)
            && descriptor.ImplementationType == typeof(FakePortForwardRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddAtomSSHSshNetSessionRegistersSshNetRuntimePorts()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICredentialResolver, StubCredentialResolver>();
        services.AddSingleton<IHostKeyTrustStore, StubHostKeyTrustStore>();
        services.AddAtomSSHSshNetSession();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISshNetConnectionInfoFactory)
            && descriptor.ImplementationType == typeof(SshNetConnectionInfoFactory)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(SshNetHostKeyVerifier)
            && descriptor.ImplementationType == typeof(SshNetHostKeyVerifier)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(SshNetClientConnector)
            && descriptor.ImplementationType == typeof(SshNetClientConnector)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(SshNetSessionRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISftpBrowser)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISftpFileTransfer)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPortForwardRuntime)
            && descriptor.ImplementationType == typeof(SshNetPortForwardRuntime)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task FakeRuntimeExposesUsableTerminalChannel()
    {
        var runtime = new FakeSshSessionRuntime();
        var profile = CreateProfile();
        var route = new ConnectionRoute(ConnectionRouteKind.Direct, profile.Endpoint, Array.Empty<JumpHostRoute>());

        var session = await runtime.OpenTerminalAsync(profile, route, CancellationToken.None);
        var channel = await runtime.GetTerminalChannelAsync(session.Value, CancellationToken.None);
        var send = await channel.Value!.SendAsync(Encoding.UTF8.GetBytes("pwd\n"), CancellationToken.None);
        var resize = await channel.Value.ResizeAsync(new TerminalSize(120, 30), CancellationToken.None);
        var buffer = new byte[128];
        var read = await channel.Value.ReadAsync(buffer, CancellationToken.None);

        Assert.True(session.Succeeded);
        Assert.True(channel.Succeeded);
        Assert.True(send.Succeeded);
        Assert.True(resize.Succeeded);
        Assert.True(read.Succeeded);
        Assert.True(read.Value > 0);
        Assert.Contains("fake terminal", Encoding.UTF8.GetString(buffer, 0, read.Value));
    }


    [Fact]
    public void SshNetConnectionInfoFactoryCreatesPasswordConnectionInfoForDirectRoute()
    {
        var profile = CreateProfile();
        var route = new ConnectionRoute(ConnectionRouteKind.Direct, profile.Endpoint, Array.Empty<JumpHostRoute>());
        var credential = new CredentialLease(
            profile.CredentialRef!.Value,
            new PasswordCredentialMaterial("secret"),
            DateTimeOffset.UtcNow);
        var factory = new SshNetConnectionInfoFactory();

        var result = factory.Create(profile, route, credential);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("example.internal", result.Value.Host);
        Assert.Equal(22, result.Value.Port);
        Assert.Equal("ops", result.Value.Username);
    }

    [Fact]
    public void SshNetConnectionInfoFactoryCreatesConnectionInfoForPlannedTargetRoute()
    {
        var profile = CreateProfile();
        var jumpHost = new JumpHostRoute(SshProfileId.New(), new SshEndpoint(new HostName("jump.internal"), 22));
        var route = new ConnectionRoute(ConnectionRouteKind.JumpHost, profile.Endpoint, new[] { jumpHost });
        var credential = new CredentialLease(
            profile.CredentialRef!.Value,
            new PasswordCredentialMaterial("secret"),
            DateTimeOffset.UtcNow);
        var factory = new SshNetConnectionInfoFactory();

        var result = factory.Create(profile, route, credential);

        Assert.True(result.Succeeded);
        Assert.Equal("example.internal", result.Value!.Host);
    }

    [Fact]
    public void SshNetConnectionInfoFactoryRejectsCredentialMaterialThatDoesNotMatchProfileAuthMethod()
    {
        var profile = CreateProfile() with { AuthMethod = SshAuthMethod.PrivateKey };
        var credential = new CredentialLease(
            profile.CredentialRef!.Value,
            new PasswordCredentialMaterial("secret"),
            DateTimeOffset.UtcNow);
        var factory = new SshNetConnectionInfoFactory();

        var result = factory.Create(profile, profile.Endpoint, credential);

        Assert.False(result.Succeeded);
        Assert.Equal(Core.Results.SshErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public void SshNetConnectionInfoFactoryCreatesPrivateKeyAuthentication()
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        var profile = CreateProfile() with { AuthMethod = SshAuthMethod.PrivateKey };
        var credential = new CredentialLease(
            profile.CredentialRef!.Value,
            new PrivateKeyCredentialMaterial(privateKeyPem, null),
            DateTimeOffset.UtcNow);
        var factory = new SshNetConnectionInfoFactory();

        var result = factory.Create(profile, profile.Endpoint, credential);

        Assert.True(result.Succeeded);
        Assert.Equal("example.internal", result.Value!.Host);
    }

    [Fact]
    public void SshNetConnectionInfoFactoryRejectsKeyboardInteractiveUntilPromptSupportExists()
    {
        var profile = CreateProfile() with { AuthMethod = SshAuthMethod.KeyboardInteractive };
        var credential = new CredentialLease(
            profile.CredentialRef!.Value,
            new KeyboardInteractiveCredentialMaterial(),
            DateTimeOffset.UtcNow);
        var factory = new SshNetConnectionInfoFactory();

        var result = factory.Create(profile, profile.Endpoint, credential);

        Assert.False(result.Succeeded);
        Assert.Equal(Core.Results.SshErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("Keyboard-interactive", result.Error!.Summary);
    }

    [Fact]
    public void HostKeyVerifierAcceptsTrustedMatchingHostKey()
    {
        var endpoint = new SshEndpoint(new HostName("example.internal"), 22);
        var fingerprint = new HostKeyFingerprint("SHA256", "abc");
        var verifier = new SshNetHostKeyVerifier(new StubHostKeyTrustStore(new KnownHostEntry(
            endpoint.Host,
            endpoint.Port,
            "ssh-ed25519",
            fingerprint,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            HostKeyTrustDecision.Trusted)));

        var result = verifier.Verify(endpoint, "ssh-ed25519", fingerprint);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void HostKeyVerifierRejectsUnknownHostKey()
    {
        var endpoint = new SshEndpoint(new HostName("example.internal"), 22);
        var verifier = new SshNetHostKeyVerifier(new StubHostKeyTrustStore(null));

        var result = verifier.Verify(endpoint, "ssh-ed25519", new HostKeyFingerprint("SHA256", "abc"));

        Assert.False(result.Succeeded);
        Assert.Equal(SshErrorKind.HostKey, result.Error?.Kind);
        Assert.Contains("unknown", result.Error!.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostKeyVerifierRejectsChangedHostKey()
    {
        var endpoint = new SshEndpoint(new HostName("example.internal"), 22);
        var verifier = new SshNetHostKeyVerifier(new StubHostKeyTrustStore(new KnownHostEntry(
            endpoint.Host,
            endpoint.Port,
            "ssh-ed25519",
            new HostKeyFingerprint("SHA256", "old"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            HostKeyTrustDecision.Trusted)));

        var result = verifier.Verify(endpoint, "ssh-ed25519", new HostKeyFingerprint("SHA256", "new"));

        Assert.False(result.Succeeded);
        Assert.Equal(SshErrorKind.HostKey, result.Error?.Kind);
        Assert.Contains("changed", result.Error!.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static SshProfile CreateProfile()
    {
        return new SshProfile(
            SshProfileId.New(),
            "example",
            new SshEndpoint(new HostName("example.internal"), 22),
            "ops",
            SshAuthMethod.Password,
            CredentialRef.New(),
            Group: null,
            TerminalProfile: null);
    }

    private sealed class StubCredentialResolver : ICredentialResolver
    {
        public Task<OperationResult<CredentialLease>> ResolveAsync(
            CredentialRef credentialRef,
            CancellationToken cancellationToken)
        {
            var lease = new CredentialLease(
                credentialRef,
                new PasswordCredentialMaterial("secret"),
                DateTimeOffset.UtcNow);

            return Task.FromResult(OperationResult<CredentialLease>.Success(lease));
        }
    }

    private sealed class StubHostKeyTrustStore : IHostKeyTrustStore
    {
        private readonly KnownHostEntry? _entry;

        public StubHostKeyTrustStore()
            : this(new KnownHostEntry(
                new HostName("example.internal"),
                22,
                "ssh-ed25519",
                new HostKeyFingerprint("SHA256", "placeholder"),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                HostKeyTrustDecision.Trusted))
        {
        }

        public StubHostKeyTrustStore(KnownHostEntry? entry)
        {
            _entry = entry;
        }

        public Task<OperationResult<KnownHostEntry?>> FindAsync(
            HostName host,
            int port,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<KnownHostEntry?>.Success(_entry));
        }

        public Task<OperationResult> SaveAsync(KnownHostEntry entry, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class StubProfileRepository : ISshProfileRepository
    {
        private readonly SshProfile _profile;

        public StubProfileRepository(SshProfile profile)
        {
            _profile = profile;
        }

        public Task<OperationResult<SshProfile?>> GetAsync(SshProfileId id, CancellationToken cancellationToken)
        {
            return Task.FromResult(OperationResult<SshProfile?>.Success(_profile));
        }

        public Task<OperationResult<IReadOnlyList<SshProfile>>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<SshProfile> profiles = [_profile];
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
}
