using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Models.Entities;

namespace PrawnsFarm.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<FarmProfile> FarmProfiles => Set<FarmProfile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>().HasIndex(u => u.Username).IsUnique();

        b.Entity<Expense>()
            .HasOne(e => e.Category)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Expense>().HasIndex(e => e.Date);
        b.Entity<Expense>().HasIndex(e => e.FarmId);
        b.Entity<Sale>().HasIndex(s => s.Date);
        b.Entity<Sale>().HasIndex(s => s.FarmId);
    }
}
