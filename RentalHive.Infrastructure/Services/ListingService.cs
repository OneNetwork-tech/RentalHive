using Microsoft.EntityFrameworkCore;
using RentalHive.Application.Contracts;
using RentalHive.Application.Dtos;
using RentalHive.Application.Listings;
using RentalHive.Domain.Common;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence;

namespace RentalHive.Infrastructure.Services;

public sealed class ListingService(RentalHiveDbContext db, IImageStore images, IClock clock) : IListingService
{
    public const int MaxImagesPerListing = 8;

    public async Task<PagedResult<ListingSummary>> SearchAsync(ListingSearch s, CancellationToken ct = default)
    {
        var page = Math.Max(1, s.Page);
        var size = Math.Clamp(s.PageSize, 1, 60);
        var q = db.RentalItems.AsNoTracking().Where(i => i.IsActive);

        if (s.Category is { } cat) q = q.Where(i => EF.Property<string>(i, "item_type") == cat.ToString());
        if (!string.IsNullOrWhiteSpace(s.City))
        {
            var city = s.City.Trim();
            q = q.Where(i => EF.Functions.ILike(i.City, EscapeLike(city) + "%", "\\"));
        }
        if (!string.IsNullOrWhiteSpace(s.Query))
        {
            var pattern = "%" + EscapeLike(s.Query.Trim()) + "%";
            q = q.Where(i => EF.Functions.ILike(i.Name, pattern, "\\") || EF.Functions.ILike(i.Description, pattern, "\\"));
        }
        if (s.MinPrice is { } min) q = q.Where(i => i.DailyRate >= min);
        if (s.MaxPrice is { } max) q = q.Where(i => i.DailyRate <= max);
        if (s.From is { } from && s.To is { } to && to > from)
        {
            q = q.Where(i => !i.Bookings.Any(b => b.Status == BookingStatus.Confirmed && b.StartDate < to && from < b.EndDate));
        }

        var total = await q.CountAsync(ct).ConfigureAwait(false);
        q = s.Sort switch
        {
            ListingSort.PriceLow => q.OrderBy(i => i.DailyRate).ThenByDescending(i => i.Id),
            ListingSort.PriceHigh => q.OrderByDescending(i => i.DailyRate).ThenByDescending(i => i.Id),
            ListingSort.Rating => q.OrderByDescending(i => i.Reviews.Average(r => (double?)r.Rating) ?? 0).ThenByDescending(i => i.Id),
            _ => q.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id),
        };
        var items = await q.Skip((page - 1) * size).Take(size)
            .Include(i => i.Images)
            .AsSplitQuery()
            .ToListAsync(ct).ConfigureAwait(false);
        var ratings = await RatingsAsync(items.Select(i => i.Id).ToList(), ct).ConfigureAwait(false);
        return new PagedResult<ListingSummary>(items.Select(i => Summary(i, ratings)).ToList(), total, page, size);
    }

    public async Task<ListingDetails?> GetAsync(int id, int? viewerId, CancellationToken ct = default)
    {
        var item = await db.RentalItems.AsNoTracking()
            .Include(i => i.Owner)
            .Include(i => i.Images)
            .Include(i => i.Reviews).ThenInclude(r => r.Author)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
        if (item is null) return null;
        // Inactive listings are visible only to their owner.
        if (!item.IsActive && item.OwnerId != viewerId) return null;

        var today = clock.Today;
        var unavailable = await db.Bookings.AsNoTracking()
            .Where(b => b.RentalItemId == id && b.Status == BookingStatus.Confirmed && b.EndDate > today)
            .OrderBy(b => b.StartDate)
            .Select(b => new DateRange(b.StartDate, b.EndDate))
            .ToListAsync(ct).ConfigureAwait(false);
        var reviews = item.Reviews.OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.Rating, r.Comment, r.Author?.DisplayName ?? "Member", r.CreatedAt))
            .ToList();
        return new ListingDetails(
            item.Id, item.Category, item.Name, item.Description, item.City, item.DailyRate, item.IsActive,
            item.OwnerId, item.Owner?.DisplayName ?? "Member", item.Owner?.MemberSince ?? default,
            item.Images.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new ImageDto(x.Id, x.FileName)).ToList(),
            ListingFactory.Specs(item),
            reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : null,
            reviews, unavailable, ListingFactory.ToInput(item));
    }

    public async Task<IReadOnlyList<ListingSummary>> GetOwnedAsync(int ownerId, CancellationToken ct = default)
    {
        var items = await db.RentalItems.AsNoTracking().Where(i => i.OwnerId == ownerId)
            .Include(i => i.Images).OrderByDescending(i => i.CreatedAt).AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);
        var ratings = await RatingsAsync(items.Select(i => i.Id).ToList(), ct).ConfigureAwait(false);
        return items.Select(i => Summary(i, ratings)).ToList();
    }

    public async Task<int> CreateAsync(int ownerId, ListingInput input, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var item = ListingFactory.Create(input, ownerId, now, clock.Today.Year);
        db.RentalItems.Add(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return item.Id;
    }

    public async Task UpdateAsync(int ownerId, int id, ListingInput input, CancellationToken ct = default)
    {
        var item = await OwnedAsync(ownerId, id, ct).ConfigureAwait(false);
        ListingFactory.Apply(input, item, clock.UtcNow, clock.Today.Year);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task SetActiveAsync(int ownerId, int id, bool active, CancellationToken ct = default)
    {
        var item = await OwnedAsync(ownerId, id, ct).ConfigureAwait(false);
        if (active && item.RemovedAt is not null)
            throw new DomainException("A moderator removed this listing, so it can't be reactivated.", DomainErrorKind.Forbidden);
        item.IsActive = active;
        item.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(int ownerId, int id, CancellationToken ct = default)
    {
        var item = await OwnedAsync(ownerId, id, ct, includeImages: true).ConfigureAwait(false);
        var today = clock.Today;
        if (await db.Bookings.AnyAsync(b => b.RentalItemId == id && b.Status == BookingStatus.Confirmed && b.EndDate > today, ct).ConfigureAwait(false))
            throw new DomainException("The listing has upcoming confirmed bookings. Cancel them before removing it.", DomainErrorKind.Conflict);

        if (await db.Bookings.AnyAsync(b => b.RentalItemId == id, ct).ConfigureAwait(false))
        {
            // Keep booking history intact: deactivate and decline open requests.
            item.IsActive = false;
            item.UpdatedAt = clock.UtcNow;
            var pending = await db.Bookings.Where(b => b.RentalItemId == id && b.Status == BookingStatus.Pending).ToListAsync(ct).ConfigureAwait(false);
            foreach (var b in pending) b.DeclineAsUnavailable(clock.UtcNow);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return false;
        }
        var files = item.Images.Select(i => i.FileName).ToList();
        db.RentalItems.Remove(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        foreach (var f in files) images.Delete(f);
        return true;
    }

    public async Task<ImageDto> AddImageAsync(int ownerId, int id, Stream content, long length, CancellationToken ct = default)
    {
        var item = await OwnedAsync(ownerId, id, ct, includeImages: true).ConfigureAwait(false);
        if (item.Images.Count >= MaxImagesPerListing) throw new DomainException($"A listing can have at most {MaxImagesPerListing} photos.");
        var stored = await images.SaveAsync(content, length, ct).ConfigureAwait(false);
        var image = new ItemImage
        {
            RentalItemId = id,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SortOrder = item.Images.Count == 0 ? 0 : item.Images.Max(i => i.SortOrder) + 1,
            CreatedAt = clock.UtcNow,
        };
        db.ItemImages.Add(image);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            images.Delete(stored.FileName);
            throw;
        }
        return new ImageDto(image.Id, image.FileName);
    }

    public async Task DeleteImageAsync(int ownerId, int id, int imageId, CancellationToken ct = default)
    {
        await OwnedAsync(ownerId, id, ct).ConfigureAwait(false);
        var image = await db.ItemImages.FirstOrDefaultAsync(i => i.Id == imageId && i.RentalItemId == id, ct).ConfigureAwait(false)
                    ?? throw new DomainException("Photo not found.", DomainErrorKind.NotFound);
        db.ItemImages.Remove(image);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        images.Delete(image.FileName);
    }

    private async Task<RentalItem> OwnedAsync(int ownerId, int id, CancellationToken ct, bool includeImages = false)
    {
        IQueryable<RentalItem> q = db.RentalItems;
        if (includeImages) q = q.Include(i => i.Images);
        var item = await q.FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
        // Same answer for "missing" and "not yours" so ids can't be probed.
        if (item is null || item.OwnerId != ownerId) throw new DomainException("Listing not found.", DomainErrorKind.NotFound);
        return item;
    }

    private async Task<Dictionary<int, (double Avg, int Count)>> RatingsAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<int, (double, int)>();
        var rows = await db.Reviews.AsNoTracking().Where(r => ids.Contains(r.RentalItemId))
            .GroupBy(r => r.RentalItemId)
            .Select(g => new { g.Key, Avg = g.Average(r => r.Rating), Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.ToDictionary(r => r.Key, r => (Math.Round(r.Avg, 1), r.Count));
    }

    private static ListingSummary Summary(RentalItem i, Dictionary<int, (double Avg, int Count)> ratings)
    {
        ratings.TryGetValue(i.Id, out var r);
        var image = i.Images.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).FirstOrDefault()?.FileName;
        return new ListingSummary(i.Id, i.Category, i.Name, i.City, i.DailyRate, image, r.Count > 0 ? r.Avg : null, r.Count, ListingFactory.Subtitle(i), i.IsActive, i.RemovalReason);
    }

    private static string EscapeLike(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
