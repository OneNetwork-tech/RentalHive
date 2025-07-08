// In RentalHive.Domain/Entities/RentalItem.cs
namespace RentalHive.Domain.Entities;
public abstract class RentalItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal DailyRate { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public int OwnerId { get; set; } // Foreign key to the User
    public User Owner { get; set; }
    public List<Booking> Bookings { get; set; } = new();
}