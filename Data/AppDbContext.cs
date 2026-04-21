using Microsoft.EntityFrameworkCore;
using CVWebsite.Models;

namespace CVWebsite.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<PendingPayment> PendingPayments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<PendingPayment>()
            .HasIndex(p => p.OrderCode)
            .IsUnique();
    }
}
