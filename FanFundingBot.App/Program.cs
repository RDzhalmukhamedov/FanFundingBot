using FanFundingBot.Application;
using FanFundingBot.App.Services;
using FanFundingBot.Bot;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Infrastructure.Persistence;
using FanFundingBot.Infrastructure.ExternalStorage;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddDataLayer(builder.Configuration);
builder.Services.AddBotLayer(builder.Configuration);
builder.Services.AddExternalStorage(builder.Configuration);

var botConfig = new BotConfiguration();
builder.Configuration.Bind("BotConfiguration", botConfig);
builder.Services.AddSingleton(botConfig);

builder.Services.AddHttpClient("telegram_bot")
    .AddTypedClient<ITelegramBotClient>((httpClient, _) =>
        new TelegramBotClient(botConfig.BotToken, httpClient));

builder.Services.AddScoped<UpdateHandler>();

builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<OutboxProcessor>();

if (botConfig.UsePolling)
    builder.Services.AddHostedService<PollingService>();
else
    builder.Services.AddHostedService<WebhookService>();

var app = builder.Build();

app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=App}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
            logger.LogInformation("Database migration completed successfully.");
            break;
        }
        catch (Exception ex) when (attempt < 3)
        {
            logger.LogWarning(ex,
                "Database migration failed (attempt {Attempt}/3). Retrying in 2s...", attempt);
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed after 3 attempts.");
            throw;
        }
    }
}

app.Run();
