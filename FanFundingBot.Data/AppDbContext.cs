using FanFundingBot.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Addon> Addons => Set<Addon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderAddon> OrderAddons => Set<OrderAddon>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ProjectStatusEntry> ProjectStatusEntries => Set<ProjectStatusEntry>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
