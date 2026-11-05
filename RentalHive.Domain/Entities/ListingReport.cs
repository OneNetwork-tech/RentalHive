namespace RentalHive.Domain.Entities;

/// <summary>A member's report that a listing breaks the rules (fake, dangerous, misleading…).</summary>
public class ListingReport
{
    public const int MaxReasonLength = 1000;

    public int Id { get; set; }
    public int RentalItemId { get; set; }
    public RentalItem? RentalItem { get; set; }
    public int ReporterId { get; set; }
    public User? Reporter { get; set; }
    public required string Reason { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>When a moderator removed the listing or dismissed the report.</summary>
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}
