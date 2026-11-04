namespace RentalHive.Domain.Entities;

/// <summary>A member who can both rent out items and rent from others.</summary>
public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    /// <summary>Keyed hash of the normalised personnummer, for uniqueness and login lookup.</summary>
    public required string PersonalNumberHash { get; set; }

    /// <summary>Encrypted (AES-GCM) normalised personnummer. Never exposed to other users.</summary>
    public required string PersonalNumberCipher { get; set; }

    public DateOnly BirthDate { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public required string PasswordHash { get; set; }

    /// <summary>Changes on password change; invalidates all existing sessions and tokens.</summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public DateTime MemberSince { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public ICollection<RentalItem> OwnedItems { get; } = new List<RentalItem>();
    public ICollection<Booking> Bookings { get; } = new List<Booking>();

    /// <summary>Public name shown to other members: first name and last initial.</summary>
    public string DisplayName => LastName.Length > 0 ? $"{FirstName} {LastName[0]}." : FirstName;
}
