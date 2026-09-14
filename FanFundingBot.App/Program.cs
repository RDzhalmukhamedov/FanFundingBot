using FanFundingBot.Application;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Users.Commands;
using FanFundingBot.App.Auth;
using FanFundingBot.App.Components;
using FanFundingBot.App.Endpoints;
using FanFundingBot.App.Services;
using FanFundingBot.Bot;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Domain.Interfaces;
using FanFundingBot.Infrastructure.Persistence;
using FanFundingBot.Infrastructure.ExternalStorage;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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

// Web-аутентификация (Telegram Login Widget -> JWT в HttpOnly-cookie).
// HybridCurrentUserProvider — после AddBotLayer, чтобы перекрыть TelegramCurrentUserProvider.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserProvider, HybridCurrentUserProvider>();

// Blazor Server: состояние аутентификации из JWT-cookie + HTTP-клиент к своему API.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddScoped<WebApiTokenAccessor>();
builder.Services.AddTransient<AuthHeaderHandler>();
builder.Services.AddHttpClient("webapi").AddHttpMessageHandler<AuthHeaderHandler>();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (!string.IsNullOrEmpty(jwtSecret))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    if (string.IsNullOrEmpty(ctx.Token)
                        && ctx.Request.Cookies.TryGetValue(JwtTokenService.CookieName, out var cookie)
                        && !string.IsNullOrEmpty(cookie))
                    {
                        ctx.Token = cookie;
                    }

                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();
}

if (botConfig.UsePolling)
    builder.Services.AddHostedService<PollingService>();
else
    builder.Services.AddHostedService<WebhookService>();

var app = builder.Build();

app.UseStaticFiles();

if (!string.IsNullOrEmpty(jwtSecret))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();

// Health (anonymous)
app.MapGet("/health", () => Results.Ok("ok"));

// Вход через Telegram Login Widget: проверка подписи -> GetOrCreateUser -> JWT в HttpOnly-cookie.
app.MapPost("/api/auth/telegram", async (
    TelegramAuthRequest request,
    IMediator mediator,
    JwtTokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    BotConfiguration botConfig,
    IUserRepository users,
    HttpContext http,
    CancellationToken ct) =>
{
        if (string.IsNullOrEmpty(jwtOptions.Value.Secret))
        return Results.Problem("JWT не настроен (Jwt:Secret пустой).", statusCode: 500);

    if (!TelegramAuthValidator.IsValid(request.ToFields(), botConfig.BotToken))
        return Results.Unauthorized();

    var login = await mediator.Send(new GetOrCreateUserCommand(
        request.Id, request.FirstName, request.LastName, request.Username,
        request.Id == botConfig.OwnerId), ct);
    if (login.IsFailed)
        return Results.BadRequest(login.Errors.Select(e => e.Message));

    var user = await users.GetByIdAsync(request.Id);
    if (user is null)
        return Results.BadRequest(new[] { "Пользователь не найден." });

    var token = tokens.CreateToken(user.Id, user.Role.ToString());
    http.Response.Cookies.Append(JwtTokenService.CookieName, token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = http.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.ExpiresDays)
    });

    return Results.Ok(new { userId = user.Id, role = user.Role.ToString() });
})
.WithName("TelegramLogin")
.WithSummary("Вход через Telegram Login Widget");

app.MapWebApi();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

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
