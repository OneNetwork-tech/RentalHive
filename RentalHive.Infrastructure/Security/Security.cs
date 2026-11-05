using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RentalHive.Application.Contracts;

namespace RentalHive.Infrastructure.Security;

public sealed class SecurityOptions
{
    public const string Section = "Security";

    /// <summary>Base64 encoded 32-byte master key for personnummer protection (keep in a secret store).</summary>
    public string PersonalNumberKey { get; set; } = string.Empty;

    public int BcryptWorkFactor { get; set; } = 12;
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}

/// <summary>
/// HMAC-SHA256 for deterministic lookups and AES-256-GCM for reversible storage.
/// Separate sub-keys are derived from the master key with HKDF.
/// </summary>
public sealed class PersonalNumberProtector : IPersonalNumberProtector
{
    private const byte FormatVersion = 1;
    private readonly byte[] _hashKey;
    private readonly byte[] _encryptionKey;

    public PersonalNumberProtector(IOptions<SecurityOptions> options)
    {
        byte[] master;
        try
        {
            master = Convert.FromBase64String(options.Value.PersonalNumberKey ?? "");
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Security:PersonalNumberKey must be base64.", ex);
        }
        if (master.Length < 32) throw new InvalidOperationException("Security:PersonalNumberKey must be a base64 encoded 32-byte key (openssl rand -base64 32).");
        _hashKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, info: Encoding.UTF8.GetBytes("rentalhive/pnr/hash"));
        _encryptionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, info: Encoding.UTF8.GetBytes("rentalhive/pnr/aes-gcm"));
    }

    public string Hash(string normalizedPersonalNumber) =>
        Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(normalizedPersonalNumber))).ToLowerInvariant();

    public string Encrypt(string normalizedPersonalNumber)
    {
        var plain = Encoding.UTF8.GetBytes(normalizedPersonalNumber);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(_encryptionKey, tag.Length);
        aes.Encrypt(nonce, plain, cipher, tag);
        var output = new byte[1 + nonce.Length + tag.Length + cipher.Length];
        output[0] = FormatVersion;
        nonce.CopyTo(output, 1);
        tag.CopyTo(output, 1 + nonce.Length);
        cipher.CopyTo(output, 1 + nonce.Length + tag.Length);
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string cipher)
    {
        var data = Convert.FromBase64String(cipher);
        if (data.Length < 1 + 12 + 16 || data[0] != FormatVersion) throw new CryptographicException("Unknown personnummer cipher format.");
        var nonce = data.AsSpan(1, 12);
        var tag = data.AsSpan(13, 16);
        var body = data.AsSpan(29);
        var plain = new byte[body.Length];
        using var aes = new AesGcm(_encryptionKey, 16);
        aes.Decrypt(nonce, body, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}

public sealed class BcryptPasswordHasher(IOptions<SecurityOptions> options) : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, options.Value.BcryptWorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Stockholm));
}
