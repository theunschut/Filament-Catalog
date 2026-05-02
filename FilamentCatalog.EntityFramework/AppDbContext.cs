using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Spool> Spools => Set<Spool>();
    public DbSet<BambuProduct> BambuProducts => Set<BambuProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Spool>()
            .HasOne(s => s.Owner)
            .WithMany()
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BambuProduct>()
            .HasIndex(p => new { p.Name, p.Material })
            .IsUnique();

        modelBuilder.Entity<BambuProduct>()
            .HasIndex(p => p.Material);

        modelBuilder.Entity<BambuProduct>()
            .HasIndex(p => p.LastSyncedAt);
    }
}
