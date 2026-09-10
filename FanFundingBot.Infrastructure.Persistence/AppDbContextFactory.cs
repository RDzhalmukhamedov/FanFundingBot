using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FanFundingBot.Infrastructure.Persistence;

/// <summary>
/// Design-time фабрика для dotnet-ef (migrations add/script/list).
/// Соединение не устанавливается при генерации миграций, строка нужна только для построения модели.
/// Реальную строку для database update задать через env ConnectionStrings__DefaultConnection.
/// </summary>
[ExcludeFromCodeCoverage]
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=fanfundingbot;Username=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
