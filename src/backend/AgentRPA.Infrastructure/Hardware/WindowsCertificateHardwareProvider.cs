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
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new HardwareOperationResult(false, "WINDOWS_ONLY", "Windows Certificate Provider 仅支持 Windows。"));
        if (string.Equals(request.Operation, "Discover", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new HardwareOperationResult(true));
        // 签名、PIN、厂商专用会话必须由具体 UKey SDK Provider 实现，平台层禁止模拟 PIN 或绕过驱动安全策略。
        return Task.FromResult(new HardwareOperationResult(false, "OPERATION_NOT_SUPPORTED", "当前 Provider 仅负责发现证书型硬件；签名/PIN 操作需接入厂商 SDK。"));
    }
}
