using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RentalHive.Application.Dtos;

namespace RentalHive.Api;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "RentalHive";
    public string Audience { get; set; } = "RentalHive.Clients";

    /// <summary>HMAC signing key, at least 32 bytes. Supply via environment/secret store.</summary>
    public string Key { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 60;

    public SymmetricSecurityKey SigningKey()
    {
        var bytes = Encoding.UTF8.GetBytes(Key);
        if (bytes.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
        return new SymmetricSecurityKey(bytes);
    }
}

public static class ClaimNames
{
    public const string SecurityStamp = "stamp";
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider time)
{
    public (string Token, DateTime ExpiresUtc) Issue(UserSummary user)
    {
        var o = options.Value;
        var now = time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(o.LifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimNames.SecurityStamp, user.SecurityStamp.ToString()),
            ]),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(o.SigningKey(), SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id (from the validated token's sub claim).</summary>
    public static int UserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
