using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Interfaces;
using FanFundingBot.Infrastructure.Persistence.Readers;
using FanFundingBot.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace FanFundingBot.Infrastructure.Persistence;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            }).AddInterceptors(sp.GetRequiredService<DomainEventInterceptor>()));

        services.AddScoped<DomainEventInterceptor>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IProjectStatusRepository, ProjectStatusRepository>();
        services.AddScoped<IAddonUsageReader, AddonUsageReader>();

        return services;
    }
}
