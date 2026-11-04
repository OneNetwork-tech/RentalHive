using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using RentalHive.Infrastructure;
using RentalHive.Infrastructure.Web;
using RentalHive.Web.Auth;
using RentalHive.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();

// Cookie keys must survive restarts and be shared between instances.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
var dp = builder.Services.AddDataProtection().SetApplicationName("RentalHive.Web");
if (!string.IsNullOrWhiteSpace(keysPath)) dp.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "rh_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.LoginPath = "/login";
        o.LogoutPath = "/logout";
        o.AccessDeniedPath = "/login";
        o.Events.OnValidatePrincipal = CookieValidator.ValidateAsync;
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "rh_af";
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// Brute-force protection on the credential forms.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var perMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 10);
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var path = ctx.Request.Path.Value ?? "";
        var isCredentialPost = HttpMethods.IsPost(ctx.Request.Method)
            && (path.Equals("/login", StringComparison.OrdinalIgnoreCase) || path.Equals("/register", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/account", StringComparison.OrdinalIgnoreCase));
        return isCredentialPost
            ? RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) })
            : RateLimitPartition.GetNoLimiter("none");
    });
});

builder.Services.AddDomainProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseSecurityHeaders(
    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
    "connect-src 'self' ws: wss:; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapMedia();
app.MapHealth();
app.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
}).WithMetadata(new Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute());
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true)) await app.MigrateDatabaseAsync();

await app.RunAsync();

/// <summary>Exposed for integration tests.</summary>
public partial class Program;
