using Microsoft.EntityFrameworkCore;

public class SummaryService(AppDbContext db) : ISummaryService
{
    public async Task<SummaryDto> GetSummaryAsync()
    {
        var meOwner = await db.Owners.FirstOrDefaultAsync(o => o.IsMe);
        var spools = await db.Spools.ToListAsync();
        var mySpoolCount = meOwner is null ? 0 : spools.Count(s => s.OwnerId == meOwner.Id);
        var totalValue = spools.Where(s => s.PricePaid.HasValue).Sum(s => s.PricePaid!.Value);
        var totalOwed = spools
            .Where(s => meOwner is not null && s.OwnerId != meOwner.Id
                        && s.PaymentStatus != PaymentStatus.Paid
                        && s.PricePaid.HasValue)
            .Sum(s => s.PricePaid!.Value);
        return new SummaryDto(spools.Count, mySpoolCount, totalValue, totalOwed);
    }

    public async Task<IEnumerable<BalanceRowDto>> GetBalanceAsync()
    {
        var nonMeOwners = await db.Owners.Where(o => !o.IsMe).OrderBy(o => o.Name).ToListAsync();
        var allSpools = await db.Spools.ToListAsync();
        return nonMeOwners.Select(owner =>
        {
            var ownerSpools = allSpools.Where(s => s.OwnerId == owner.Id).ToList();
            var value = ownerSpools.Where(s => s.PricePaid.HasValue).Sum(s => s.PricePaid!.Value);
            var owed = ownerSpools
                .Where(s => s.PaymentStatus != PaymentStatus.Paid && s.PricePaid.HasValue)
                .Sum(s => s.PricePaid!.Value);
            var hasUnpriced = ownerSpools.Any(s => !s.PricePaid.HasValue);
            return new BalanceRowDto(owner.Id, owner.Name, ownerSpools.Count, value, owed, hasUnpriced);
        });
    }
}
