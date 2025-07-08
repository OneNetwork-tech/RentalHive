namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a user in the system, who can be both an owner of rental items and a renter.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public required string PersonalIdentityNumber { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Address { get; set; } // Could be a simple string or a complex type
        public  string HashedPassword { get; set; }
        public DateTime MemberSince { get; set; }

        public virtual ICollection<RentalItem> OwnedItems { get; set; } = new List<RentalItem>();
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
