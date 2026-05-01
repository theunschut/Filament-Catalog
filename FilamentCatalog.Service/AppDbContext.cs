using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Spool> Spools => Set<Spool>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Spool>()
            .HasOne(s => s.Owner)
            .WithMany()
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
