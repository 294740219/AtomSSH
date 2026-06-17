using System.ComponentModel;
using System.Runtime.InteropServices;
using AtomSSH.Core.Results;

namespace AtomSSH.Infrastructure.Credentials;

internal sealed class WindowsDpapiSecretProtector
{
    public OperationResult<byte[]> Protect(byte[] plaintext)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult<byte[]>.Failure(UnsupportedPlatformError());
        }

        return InvokeDpapi(plaintext, protect: true);
    }

    public OperationResult<byte[]> Unprotect(byte[] protectedData)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult<byte[]>.Failure(UnsupportedPlatformError());
        }

        return InvokeDpapi(protectedData, protect: false);
    }

    private static OperationResult<byte[]> InvokeDpapi(byte[] input, bool protect)
    {
        var inputBlob = default(DataBlob);
        var outputBlob = default(DataBlob);

        try
        {
            inputBlob = DataBlob.FromBytes(input);
            var ok = protect
                ? CryptProtectData(
                    ref inputBlob,
                    "AtomSSH credential secret",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    out outputBlob)
                : CryptUnprotectData(
                    ref inputBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    out outputBlob);

            if (!ok)
            {
                var exception = new Win32Exception(Marshal.GetLastWin32Error());
                return OperationResult<byte[]>.Failure(new SshError(
                    SshErrorKind.Configuration,
                    protect
                        ? "Credential secret could not be protected by Windows."
                        : "Credential secret could not be unprotected by Windows.",
                    SshErrorRedactor.RedactDetail(exception.Message)));
            }

            return OperationResult<byte[]>.Success(outputBlob.ToBytes());
        }
        finally
        {
            inputBlob.FreeHGlobal();
            outputBlob.FreeLocal();
        }
    }

    private static SshError UnsupportedPlatformError()
    {
        return new SshError(
            SshErrorKind.Configuration,
            "Credential secret storage is not implemented for this operating system.",
            "Only Windows DPAPI credential storage is available in this build.");
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Count;
        public IntPtr Data;

        public static DataBlob FromBytes(byte[] bytes)
        {
            var blob = new DataBlob
            {
                Count = bytes.Length,
                Data = Marshal.AllocHGlobal(bytes.Length)
            };

            Marshal.Copy(bytes, 0, blob.Data, bytes.Length);
            return blob;
        }

        public byte[] ToBytes()
        {
            if (Data == IntPtr.Zero || Count <= 0)
            {
                return [];
            }

            var bytes = new byte[Count];
            Marshal.Copy(Data, bytes, 0, Count);
            return bytes;
        }

        public void FreeHGlobal()
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(Data);
            Data = IntPtr.Zero;
            Count = 0;
        }

        public void FreeLocal()
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            _ = LocalFree(Data);
            Data = IntPtr.Zero;
            Count = 0;
        }
    }
}
