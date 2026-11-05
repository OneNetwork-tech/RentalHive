using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using RentalHive.Api;
using RentalHive.Application.Contracts;
using RentalHive.Infrastructure;
using RentalHive.Infrastructure.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.Key) >= 32, "Jwt:Key must be at least 32 bytes (set Jwt__Key).")
    .ValidateOnStart();
builder.Services.AddSingleton<TokenService>();

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = System.Text.Encoding.UTF8.GetByteCount(jwt.Key) >= 32 ? jwt.SigningKey() : null,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "name",
        };
        o.Events = new JwtBearerEvents
        {
            // Reject tokens issued before a password change (security stamp rotated).
            OnTokenValidated = async ctx =>
            {
                var accounts = ctx.HttpContext.RequestServices.GetRequiredService<IAccountService>();
                var sub = ctx.Principal?.FindFirst("sub")?.Value;
                var stamp = ctx.Principal?.FindFirst(ClaimNames.SecurityStamp)?.Value;
                if (!int.TryParse(sub, System.Globalization.CultureInfo.InvariantCulture, out var userId) || !Guid.TryParse(stamp, out var guid)
                    || !await accounts.IsSessionValidAsync(userId, guid, ctx.HttpContext.RequestAborted))
                {
                    ctx.Fail("Token has been revoked.");
                }
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimits.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 10), Window = TimeSpan.FromMinutes(1) }));
});

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (corsOrigins.Length > 0) p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDomainProblemDetails();
builder.Services.AddOpenApi();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    // CIDR ranges, e.g. 172.16.0.0/12 for a reverse proxy on the Docker host.
    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Configuration.GetValue("Security:HttpsOnly", !app.Environment.IsDevelopment()))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseSecurityHeaders("default-src 'none'; frame-ancestors 'none'");
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapMedia();
app.MapHealth();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true)) await app.MigrateDatabaseAsync();

await app.RunAsync();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;

namespace RentalHive.Api
{
    public static class RateLimits
    {
        public const string Auth = "auth";
    }
}
