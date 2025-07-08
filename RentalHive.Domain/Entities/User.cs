namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a user in the system, who can be both an owner of rental items and a renter.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string PersonalIdentityNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; } // Could be a simple string or a complex type
        public string HashedPassword { get; set; }
        public DateTime MemberSince { get; set; }

        public virtual ICollection<RentalItem> OwnedItems { get; set; } = new List<RentalItem>();
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
