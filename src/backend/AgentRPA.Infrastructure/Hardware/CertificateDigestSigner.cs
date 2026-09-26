using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AgentRPA.Application.Abstractions;

namespace AgentRPA.Infrastructure.Hardware;

/// <summary>使用 Windows 驱动提供的证书私钥签名 SHA-256 摘要；不接收或保存 PIN。</summary>
public static class CertificateDigestSigner
{
    public static HardwareOperationResult SignSha256(X509Certificate2 certificate, ReadOnlySpan<byte> digest)
    {
        if (digest.Length != 32) return new(false, "INVALID_SIGN_REQUEST", "SHA-256 摘要必须为 32 字节。");
        if (!certificate.HasPrivateKey || DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() || DateTime.UtcNow > certificate.NotAfter.ToUniversalTime())
            return new(false, "CERTIFICATE_UNAVAILABLE", "证书已过期或无可用私钥。");
        var usage = certificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        if (usage is not null && !usage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
            return new(false, "CERTIFICATE_USAGE_DENIED", "证书不允许数字签名。");
        try
        {
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is not null)
                return new(true, SignatureBase64: Convert.ToBase64String(rsa.SignHash(digest.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)));
            using var ecdsa = certificate.GetECDsaPrivateKey();
            if (ecdsa is not null)
                return new(true, SignatureBase64: Convert.ToBase64String(ecdsa.SignHash(digest)));
            return new(false, "ALGORITHM_UNSUPPORTED", "仅支持 RSA 或 ECDSA 证书。");
        }
        catch (CryptographicException)
        {
            return new(false, "SIGN_FAILED", "证书签名失败，请检查设备、驱动和交互式授权。");
        }
    }
}
