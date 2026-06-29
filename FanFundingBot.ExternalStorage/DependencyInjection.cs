using System.Diagnostics.CodeAnalysis;
using FanFundingBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FanFundingBot.ExternalStorage;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddExternalStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var googleSheetsOptions = new GoogleSheetsOptions();
        configuration.Bind("GoogleSheets", googleSheetsOptions);

        services.AddSingleton(googleSheetsOptions);

        if (!string.IsNullOrEmpty(googleSheetsOptions.SpreadsheetId) &&
            !string.IsNullOrEmpty(googleSheetsOptions.CredentialsJson))
        {
            services.AddScoped<IOrderExternalStorage, GoogleSheetsOrderStorage>();
        }
        else
        {
            services.AddScoped<IOrderExternalStorage, FileOrderStorage>();
        }

        return services;
    }
}
