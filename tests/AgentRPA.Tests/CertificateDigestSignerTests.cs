using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AgentRPA.Infrastructure.Hardware;

namespace AgentRPA.Tests;

public sealed class CertificateDigestSignerTests
{
    [Fact]
    public void Rsa_certificate_signs_sha256_digest_that_public_key_can_verify()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Temporary Test RSA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(30));
        var digest = SHA256.HashData("authorized challenge"u8);

        var result = CertificateDigestSigner.SignSha256(certificate, digest);

        Assert.True(result.Success);
        Assert.NotNull(result.SignatureBase64);
        using var publicKey = certificate.GetRSAPublicKey();
        Assert.True(publicKey!.VerifyHash(digest, Convert.FromBase64String(result.SignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Ecdsa_certificate_signs_sha256_digest_that_public_key_can_verify()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Temporary Test ECDSA", ecdsa, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(30));
        var digest = SHA256.HashData("authorized challenge"u8);

        var result = CertificateDigestSigner.SignSha256(certificate, digest);

        Assert.True(result.Success);
        Assert.NotNull(result.SignatureBase64);
        using var publicKey = certificate.GetECDsaPublicKey();
        Assert.True(publicKey!.VerifyHash(digest, Convert.FromBase64String(result.SignatureBase64)));
    }

    [Fact]
    public void Certificate_without_digital_signature_usage_is_rejected()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=No Signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(30));

        var result = CertificateDigestSigner.SignSha256(certificate, SHA256.HashData("challenge"u8));

        Assert.False(result.Success);
        Assert.Equal("CERTIFICATE_USAGE_DENIED", result.ErrorCode);
        Assert.Null(result.SignatureBase64);
    }

    [Fact]
    public void Invalid_digest_length_is_rejected_before_private_key_access()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Temporary Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(30));

        var result = CertificateDigestSigner.SignSha256(certificate, new byte[31]);

        Assert.False(result.Success);
        Assert.Equal("INVALID_SIGN_REQUEST", result.ErrorCode);
    }
}
