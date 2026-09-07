using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FanFundingBot.Infrastructure.Persistence;

/// <summary>
/// Design-time фабрика для dotnet-ef (migrations add/script/list).
/// Соединение не устанавливается при генерации миграций, строка нужна только для построения модели.
/// </summary>
[ExcludeFromCodeCoverage]
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=fanfundingbot;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
