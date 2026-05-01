using Microsoft.EntityFrameworkCore;

public class OwnerService(AppDbContext db) : IOwnerService
{
    public Task<List<Owner>> GetAllAsync() =>
        db.Owners.OrderBy(o => o.CreatedAt).ToListAsync();

    public async Task<Owner> CreateAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Name is required.");
        var owner = new Owner { Name = name.Trim(), IsMe = false, CreatedAt = DateTime.UtcNow };
        db.Owners.Add(owner);
        await db.SaveChangesAsync();
        return owner;
    }

    public async Task DeleteAsync(int id)
    {
        var owner = await db.Owners.FindAsync(id)
            ?? throw new NotFoundException("Owner not found.");
        if (owner.IsMe)
            throw new DomainValidationException("Cannot delete the 'Me' owner.");
        var spoolCount = await db.Spools.CountAsync(s => s.OwnerId == id);
        if (spoolCount > 0)
            throw new ConflictException($"Cannot delete — {spoolCount} spool(s) assigned. Remove spools first.");
        db.Owners.Remove(owner);
        await db.SaveChangesAsync();
    }
}
