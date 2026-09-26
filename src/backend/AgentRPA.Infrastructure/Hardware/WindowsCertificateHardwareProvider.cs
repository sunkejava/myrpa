using System.Security.Cryptography.X509Certificates;
using AgentRPA.Application.Abstractions;

namespace AgentRPA.Infrastructure.Hardware;

/// <summary>Windows 证书存储硬件 Provider。适用于由 UKey/智能卡驱动暴露到 Windows Certificate Store 的证书型设备。</summary>
public sealed class WindowsCertificateHardwareProvider : IHardwareCredentialProvider
{
    public string ProviderId => "Windows.CertificateStore";

    public Task<IReadOnlyList<HardwareDeviceInfo>> DiscoverAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult<IReadOnlyList<HardwareDeviceInfo>>([]);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var devices = store.Certificates.Cast<X509Certificate2>()
            .Where(x => x.HasPrivateKey)
            .Select(x => new HardwareDeviceInfo($"cert:{x.Thumbprint}", ProviderId, "Ready", x.Subject))
            .ToArray();
        return Task.FromResult<IReadOnlyList<HardwareDeviceInfo>>(devices);
    }

    public Task<HardwareOperationResult> ExecuteAsync(HardwareOperationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new HardwareOperationResult(false, "WINDOWS_ONLY", "Windows Certificate Provider 仅支持 Windows。"));
        if (string.Equals(request.Operation, "Discover", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new HardwareOperationResult(true));
        if (!string.Equals(request.Operation, "SignDigestSha256", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new HardwareOperationResult(false, "OPERATION_NOT_SUPPORTED", "不支持该证书操作。"));
        if (!request.Parameters.TryGetValue("certificateThumbprint", out var certificateId) || certificateId is not string thumbprint ||
            thumbprint.Length is not (40 or 64) || !thumbprint.All(Uri.IsHexDigit) ||
            !request.Parameters.TryGetValue("digestBase64", out var digestValue) || digestValue is not string encoded || encoded.Length > 128)
            return Task.FromResult(new HardwareOperationResult(false, "INVALID_SIGN_REQUEST", "证书指纹或 SHA-256 摘要无效。"));
        byte[] digest;
        try { digest = Convert.FromBase64String(encoded); }
        catch (FormatException) { return Task.FromResult(new HardwareOperationResult(false, "INVALID_SIGN_REQUEST", "SHA-256 摘要不是有效 Base64。")); }
        if (digest.Length != 32) return Task.FromResult(new HardwareOperationResult(false, "INVALID_SIGN_REQUEST", "SHA-256 摘要必须为 32 字节。"));
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
            if (matches.Count != 1)
                return Task.FromResult(new HardwareOperationResult(false, "CERTIFICATE_UNAVAILABLE", "证书不存在、已过期或无可用私钥。"));
            using var certificate = matches[0];
            return Task.FromResult(CertificateDigestSigner.SignSha256(certificate, digest));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // 驱动或 Windows 密钥提供程序可要求交互式 PIN；绝不将 PIN 或驱动详细错误写入结果。
            return Task.FromResult(new HardwareOperationResult(false, "SIGN_FAILED", "证书签名失败，请检查设备、驱动和交互式授权。"));
        }
    }
}
