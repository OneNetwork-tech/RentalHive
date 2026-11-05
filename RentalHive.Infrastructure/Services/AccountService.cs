using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence;
using RentalHive.Infrastructure.Security;

namespace RentalHive.Infrastructure.Services;

public sealed class AccountService(
    RentalHiveDbContext db,
    IPasswordHasher hasher,
    IPersonalNumberProtector protector,
    IClock clock,
    IOptions<SecurityOptions> options) : IAccountService
{
    public const int MinimumAge = 18;

    // Constant-cost dummy hash so unknown users take as long as wrong passwords.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.EnhancedHashPassword("not-a-real-password", 12);

    public async Task<UserSummary> RegisterAsync(RegisterRequest r, CancellationToken ct = default)
    {
        if (!PersonalNumber.TryParse(r.PersonalNumber, clock.Today, out var pnr) || pnr is null)
            throw new DomainException("Invalid personnummer. Use the format YYYYMMDD-XXXX.");
        if (pnr.AgeOn(clock.Today) < MinimumAge)
            throw new DomainException($"You must be at least {MinimumAge} years old to register.");
        if (!PhoneNumber.TryNormalize(r.PhoneNumber, out var phone))
            throw new DomainException("Invalid phone number.");
        var email = NormalizeEmail(r.Email);
        ValidatePassword(r.Password);

        var hash = protector.Hash(pnr.Value);
        if (await db.Users.AnyAsync(u => u.Email == email, ct).ConfigureAwait(false))
            throw new DomainException("An account with this email already exists.", DomainErrorKind.Conflict);
        if (await db.Users.AnyAsync(u => u.PersonalNumberHash == hash, ct).ConfigureAwait(false))
            throw new DomainException("An account with this personnummer already exists.", DomainErrorKind.Conflict);
        if (await db.Users.AnyAsync(u => u.PhoneNumber == phone, ct).ConfigureAwait(false))
            throw new DomainException("An account with this phone number already exists.", DomainErrorKind.Conflict);

        var user = new User
        {
            Email = email,
            PhoneNumber = phone,
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            PersonalNumberHash = hash,
            PersonalNumberCipher = protector.Encrypt(pnr.Value),
            BirthDate = pnr.BirthDate,
            Address = (r.Address ?? "").Trim(),
            City = r.City.Trim(),
            PasswordHash = hasher.Hash(r.Password),
            MemberSince = clock.UtcNow,
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Lost a race with a concurrent registration.
            throw new DomainException("An account with these details already exists.", DomainErrorKind.Conflict);
        }
        return Summary(user);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest r, CancellationToken ct = default)
    {
        var user = await FindByIdentifierAsync(r.LoginIdentifier, ct).ConfigureAwait(false);
        var now = clock.UtcNow;
        if (user is null)
        {
            hasher.Verify(r.Password, DummyHash);
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }
        if (user.LockoutEndUtc is { } until && until > now)
        {
            hasher.Verify(r.Password, DummyHash);
            return new LoginResult(LoginOutcome.LockedOut, null, until);
        }
        if (!hasher.Verify(r.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= options.Value.MaxFailedLogins)
            {
                user.LockoutEndUtc = now.AddMinutes(options.Value.LockoutMinutes);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        // Only reveal a suspension to someone who knows the password.
        if (user.IsSuspended) return new LoginResult(LoginOutcome.Suspended, null);
        return new LoginResult(LoginOutcome.Success, Summary(user));
    }

    private async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        var id = (identifier ?? "").Trim();
        if (id.Length == 0) return null;
        if (id.Contains('@', StringComparison.Ordinal))
        {
            var email = id.ToLowerInvariant();
            return await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct).ConfigureAwait(false);
        }
        // Some mobile numbers (e.g. 070123-4567) are also valid personnummer, so try both.
        if (PersonalNumber.TryParse(id, clock.Today, out var pnr) && pnr is not null)
        {
            var hash = protector.Hash(pnr.Value);
            var byPnr = await db.Users.FirstOrDefaultAsync(u => u.PersonalNumberHash == hash, ct).ConfigureAwait(false);
            if (byPnr is not null) return byPnr;
        }
        if (PhoneNumber.TryNormalize(id, out var phone))
            return await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct).ConfigureAwait(false);
        return null;
    }

    public async Task<UserSummary?> FindAsync(int userId, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct).ConfigureAwait(false);
        return u is null ? null : Summary(u);
    }

    public async Task<ProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct).ConfigureAwait(false)
                ?? throw new DomainException("User not found.", DomainErrorKind.NotFound);
        return Profile(u);
    }

    public async Task<ProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest r, CancellationToken ct = default)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct).ConfigureAwait(false)
                ?? throw new DomainException("User not found.", DomainErrorKind.NotFound);
        if (!PhoneNumber.TryNormalize(r.PhoneNumber, out var phone)) throw new DomainException("Invalid phone number.");
        if (phone != u.PhoneNumber && await db.Users.AnyAsync(x => x.PhoneNumber == phone && x.Id != userId, ct).ConfigureAwait(false))
            throw new DomainException("This phone number is used by another account.", DomainErrorKind.Conflict);
        u.FirstName = r.FirstName.Trim();
        u.LastName = r.LastName.Trim();
        u.PhoneNumber = phone;
        u.Address = (r.Address ?? "").Trim();
        u.City = r.City.Trim();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Profile(u);
    }

    public async Task<UserSummary> ChangePasswordAsync(int userId, ChangePasswordRequest r, CancellationToken ct = default)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct).ConfigureAwait(false)
                ?? throw new DomainException("User not found.", DomainErrorKind.NotFound);
        if (!hasher.Verify(r.CurrentPassword, u.PasswordHash)) throw new DomainException("The current password is incorrect.");
        ValidatePassword(r.NewPassword);
        u.PasswordHash = hasher.Hash(r.NewPassword);
        u.SecurityStamp = Guid.NewGuid();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Summary(u);
    }

    public Task<bool> IsSessionValidAsync(int userId, Guid securityStamp, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Id == userId && u.SecurityStamp == securityStamp && u.SuspendedAt == null, ct);

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 10) throw new DomainException("The password must be at least 10 characters.");
        if (password.Length > 200) throw new DomainException("The password is too long.");
    }

    private static string NormalizeEmail(string email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (e.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(e, out var parsed) || parsed.Address != e)
            throw new DomainException("Invalid email address.");
        return e;
    }

    private static UserSummary Summary(User u) => new(u.Id, u.Email, u.FirstName, u.LastName, u.DisplayName, u.SecurityStamp, u.IsAdmin);

    private ProfileDto Profile(User u)
    {
        var pnr = protector.Decrypt(u.PersonalNumberCipher);
        return new ProfileDto(u.Id, u.Email, u.PhoneNumber, u.FirstName, u.LastName, u.Address, u.City, $"{pnr[..8]}-****", u.MemberSince);
    }
}

internal static class DbExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };

    public static bool IsExclusionViolation(this DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23P01" };
}
