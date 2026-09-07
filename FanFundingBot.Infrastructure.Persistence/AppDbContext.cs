using FanFundingBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Addon> Addons => Set<Addon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderPosition> OrderPositions => Set<OrderPosition>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ProjectStatus> ProjectStatusEntries => Set<ProjectStatus>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
