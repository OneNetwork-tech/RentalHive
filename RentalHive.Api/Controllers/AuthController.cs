using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;

namespace RentalHive.Api.Controllers;

public sealed record TokenResponse(string AccessToken, DateTime ExpiresUtc, UserSummaryResponse User);

public sealed record UserSummaryResponse(int Id, string Email, string FirstName, string LastName, string DisplayName)
{
    public static UserSummaryResponse From(UserSummary u) => new(u.Id, u.Email, u.FirstName, u.LastName, u.DisplayName);
}

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimits.Auth)]
public sealed class AuthController(IAccountService accounts, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await accounts.RegisterAsync(request, ct);
        var (token, expires) = tokens.Issue(user);
        return StatusCode(StatusCodes.Status201Created, new TokenResponse(token, expires, UserSummaryResponse.From(user)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await accounts.LoginAsync(request, ct);
        return result.Outcome switch
        {
            LoginOutcome.Success => Ok(new TokenResponse(tokens.Issue(result.User!).Token, tokens.Issue(result.User!).ExpiresUtc, UserSummaryResponse.From(result.User!))),
            LoginOutcome.LockedOut => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Too many failed attempts. Try again later."),
            _ => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials."),
        };
    }
}

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IAccountService accounts, TokenService tokens) : ControllerBase
{
    [HttpGet]
    public Task<ProfileDto> Get(CancellationToken ct) => accounts.GetProfileAsync(User.UserId(), ct);

    [HttpPut]
    public Task<ProfileDto> Update(UpdateProfileRequest request, CancellationToken ct) => accounts.UpdateProfileAsync(User.UserId(), request, ct);

    /// <summary>Changes the password. All previously issued tokens stop working; a new token is returned.</summary>
    [HttpPost("password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<TokenResponse> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await accounts.ChangePasswordAsync(User.UserId(), request, ct);
        var (token, expires) = tokens.Issue(user);
        return new TokenResponse(token, expires, UserSummaryResponse.From(user));
    }
}
