using AtomSSH.Core.Results;
using AtomSSH.Core.ValueObjects;

namespace AtomSSH.Core.Tests;

public sealed class CoreContractTests
{
    [Fact]
    public void OperationResultSuccessContainsValue()
    {
        var id = SshProfileId.New();

        var result = OperationResult<SshProfileId>.Success(id);

        Assert.True(result.Succeeded);
        Assert.Equal(id, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void OperationResultFailureContainsError()
    {
        var error = new SshError(SshErrorKind.Network, "Target is unreachable", IsRetryable: true);

        var result = OperationResult<SshProfileId>.Failure(error);

        Assert.False(result.Succeeded);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void SshErrorRedactorRemovesSecretDetails()
    {
        var detail = "password=hunter2 passphrase:open-sesame token=abc ssh://ops:secret@example -----BEGIN PRIVATE KEY-----abc-----END PRIVATE KEY-----";

        var redacted = SshErrorRedactor.RedactDetail(detail);

        Assert.DoesNotContain("hunter2", redacted);
        Assert.DoesNotContain("open-sesame", redacted);
        Assert.DoesNotContain("abc-----END", redacted);
        Assert.DoesNotContain("ops:secret", redacted);
        Assert.Contains("password=<redacted>", redacted);
        Assert.Contains("passphrase=<redacted>", redacted);
        Assert.Contains("[redacted-private-key]", redacted);
    }

    [Fact]
    public void ValueObjectsRejectEmptyValues()
    {
        Assert.Throws<ArgumentException>(() => new HostName(""));
        Assert.Throws<ArgumentException>(() => new RemotePath(" "));
        Assert.Throws<ArgumentException>(() => new LocalPath(""));
        Assert.Throws<ArgumentException>(() => new SshProfileId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new CredentialRef(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new TransferTaskId(Guid.Empty));
    }
}
