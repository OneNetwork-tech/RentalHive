namespace RentalHive.Domain.Entities
{
    /// <summary>
    /// Represents a booking transaction in the system.
    /// It links a user (renter) to a rental item for a specific period.
    /// </summary>
    public class Booking
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; } // e.g., "Confirmed", "Pending", "Cancelled", "Completed"

        // Foreign key to the rented item
        public int RentalItemId { get; set; }
        public virtual RentalItem RentalItem { get; set; }

        // Foreign key to the user who is renting
        public int RenterId { get; set; }
        public virtual User Renter { get; set; }
    }
}
