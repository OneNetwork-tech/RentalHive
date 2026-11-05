using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;

namespace RentalHive.Web.Auth;

public static class WebClaims
{
    public const string SecurityStamp = "rh:stamp";

    public const string AdminRole = "Admin";

    public static ClaimsPrincipal Principal(UserSummary user)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(SecurityStamp, user.SecurityStamp.ToString()),
        ];
        // Granting or revoking admin rotates the security stamp, so a stale role can't outlive its session.
        if (user.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, AdminRole));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static int? UserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : null;

    public static async Task SignInAsync(HttpContext http, UserSummary user)
    {
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Principal(user),
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });
    }
}

/// <summary>Rejects cookies whose security stamp no longer matches (e.g. after a password change).</summary>
public static class CookieValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        var principal = ctx.Principal;
        var accounts = ctx.HttpContext.RequestServices.GetRequiredService<IAccountService>();
        if (principal?.UserId() is not { } id || !Guid.TryParse(principal.FindFirstValue(WebClaims.SecurityStamp), out var stamp)
            || !await accounts.IsSessionValidAsync(id, stamp, ctx.HttpContext.RequestAborted))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

/// <summary>Re-checks interactive (SignalR) sessions periodically, since cookies are only validated on HTTP requests.</summary>
public sealed class RevalidatingAuthStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopes)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(10);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var user = authenticationState.User;
        if (user.UserId() is not { } id || !Guid.TryParse(user.FindFirstValue(WebClaims.SecurityStamp), out var stamp)) return false;
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountService>().IsSessionValidAsync(id, stamp, cancellationToken);
    }
}
