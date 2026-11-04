using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Web.Auth;

namespace RentalHive.Web;

public static class Ui
{
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>1250 → "1 250 kr", 99.5 → "99,50 kr".</summary>
    public static string Sek(decimal amount) =>
        amount == decimal.Truncate(amount)
            ? string.Create(Sv, $"{amount:#,0} kr")
            : string.Create(Sv, $"{amount:#,0.00} kr");

    public static string Date(DateOnly d) => d.ToString("ddd d MMM yyyy", CultureInfo.GetCultureInfo("en-GB"));

    public static string Image(string? fileName, ItemCategory category) =>
        fileName is null ? $"img/{category.ToString().ToLowerInvariant()}.svg" : $"media/{fileName}";

    public static string StatusLabel(BookingStatus s) => s switch
    {
        BookingStatus.Pending => "Awaiting owner",
        BookingStatus.Confirmed => "Confirmed",
        BookingStatus.Declined => "Declined",
        BookingStatus.Cancelled => "Cancelled",
        BookingStatus.Completed => "Completed",
        _ => s.ToString(),
    };

    public static string StatusClass(BookingStatus s) => s switch
    {
        BookingStatus.Pending => "badge-amber",
        BookingStatus.Confirmed => "badge-green",
        BookingStatus.Completed => "badge-blue",
        _ => "badge-gray",
    };

    public static string Stars(double rating) =>
        new string('★', (int)Math.Round(rating)) + new string('☆', 5 - (int)Math.Round(rating));
}

/// <summary>
/// Base for interactive components. Each service call runs in its own DI scope,
/// so a long-lived circuit never shares one DbContext across concurrent operations.
/// </summary>
public abstract class AppComponentBase : ComponentBase
{
    [Inject] protected IServiceScopeFactory Scopes { get; set; } = default!;
    [CascadingParameter] protected Task<AuthenticationState>? AuthState { get; set; }

    protected string? Error { get; set; }
    protected string? Notice { get; set; }
    protected bool Busy { get; set; }

    protected async Task<int?> CurrentUserIdAsync() =>
        AuthState is null ? null : (await AuthState).User.UserId();

    protected async Task<TResult> Use<TService, TResult>(Func<TService, Task<TResult>> action) where TService : notnull
    {
        await using var scope = Scopes.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    protected async Task Use<TService>(Func<TService, Task> action) where TService : notnull
    {
        await using var scope = Scopes.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    /// <summary>Run an action, showing business-rule errors to the user instead of crashing the circuit.</summary>
    protected async Task Guard(Func<Task> action, string? success = null)
    {
        Error = null;
        Notice = null;
        Busy = true;
        try
        {
            await action();
            Notice = success;
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}
