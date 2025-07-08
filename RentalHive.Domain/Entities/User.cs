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
        public string HashedPassword { get; set; }
        public DateTime MemberSince { get; set; }

        // Navigation property for items owned by the user
        public virtual ICollection<RentalItem> OwnedItems { get; set; } = new List<RentalItem>();

        // Navigation property for items rented by the user
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
