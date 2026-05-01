using Microsoft.EntityFrameworkCore;

public class SpoolService(AppDbContext db) : ISpoolService
{
    public Task<List<Spool>> GetAllAsync() =>
        db.Spools.Include(s => s.Owner).OrderBy(s => s.CreatedAt).ToListAsync();

    public async Task<Spool> CreateAsync(SpoolCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(req.Material))
            throw new DomainValidationException("Material is required.");
        if (!await db.Owners.AnyAsync(o => o.Id == req.OwnerId))
            throw new DomainValidationException("Owner not found.");

        var spool = new Spool
        {
            Name = req.Name.Trim(),
            Material = req.Material.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(req.ColorHex) ? "#888888" : req.ColorHex,
            OwnerId = req.OwnerId,
            WeightGrams = req.WeightGrams,
            PricePaid = req.PricePaid,
            PaymentStatus = req.PaymentStatus,
            SpoolStatus = req.SpoolStatus,
            Notes = req.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.Spools.Add(spool);
        await db.SaveChangesAsync();
        return spool;
    }

    public async Task<Spool> UpdateAsync(int id, SpoolUpdateRequest req)
    {
        var spool = await db.Spools.FindAsync(id)
            ?? throw new NotFoundException("Spool not found.");
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(req.Material))
            throw new DomainValidationException("Material is required.");
        if (!await db.Owners.AnyAsync(o => o.Id == req.OwnerId))
            throw new DomainValidationException("Owner not found.");

        spool.Name = req.Name.Trim();
        spool.Material = req.Material.Trim();
        spool.ColorHex = string.IsNullOrWhiteSpace(req.ColorHex) ? "#888888" : req.ColorHex;
        spool.OwnerId = req.OwnerId;
        spool.WeightGrams = req.WeightGrams;
        spool.PricePaid = req.PricePaid;
        spool.PaymentStatus = req.PaymentStatus;
        spool.SpoolStatus = req.SpoolStatus;
        spool.Notes = req.Notes?.Trim();
        await db.SaveChangesAsync();
        return spool;
    }

    public async Task DeleteAsync(int id)
    {
        var spool = await db.Spools.FindAsync(id)
            ?? throw new NotFoundException("Spool not found.");
        db.Spools.Remove(spool);
        await db.SaveChangesAsync();
    }
}
