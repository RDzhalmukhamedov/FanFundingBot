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

builder.Services.AddScoped<ProjectSheetLinkService>();

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
builder.Services.AddScoped<BlazorCircuitUserContext>();
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
                },
                // Stateless sliding refresh без refresh-таблицы: если до истечения
                // осталось меньше половины срока — перевыпустить cookie со свежей ролью.
                // Граница: продление никогда не должно ронять аутентификацию.
                OnTokenValidated = async ctx =>
                {
                    try
                    {
                        var options = ctx.HttpContext.RequestServices
                            .GetRequiredService<IOptions<JwtOptions>>().Value;
                        var validTo = ctx.SecurityToken?.ValidTo;
                        if (validTo is null
                            || !SlidingRefreshPolicy.ShouldReissue(
                                new DateTimeOffset(validTo.Value, TimeSpan.Zero),
                                DateTimeOffset.UtcNow,
                                options.ExpiresDays))
                        {
                            return;
                        }

                        var sub = ctx.Principal?.FindFirst(JwtTokenService.SubjectClaim)?.Value;
                        if (!long.TryParse(sub, out var id) || id <= 0)
                            return;

                        var users = ctx.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        var tokens = ctx.HttpContext.RequestServices.GetRequiredService<JwtTokenService>();
                        var user = await users.GetByIdAsync(id);
                        if (user is null)
                            return;

                        AuthCookieWriter.Append(
                            ctx.HttpContext.Response,
                            tokens.CreateToken(user.Id, user.Role.ToString()),
                            options.ExpiresDays,
                            ctx.HttpContext.Request.IsHttps);
                    }
                    catch
                    {
                        // Продление — best effort, ошибка игнорируется.
                    }
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
    AuthCookieWriter.Append(http.Response, token, jwtOptions.Value.ExpiresDays, http.Request.IsHttps);

    return Results.Ok(new { userId = user.Id, role = user.Role.ToString() });
})
.WithName("TelegramLogin")
.WithSummary("Вход через Telegram Login Widget");

// Выход: удаление HttpOnly-cookie (идемпотентный). Circuit дополнительно
// сбрасывается на клиенте через NotifyAuthenticationStateChanged.
app.MapPost("/api/auth/logout", (HttpContext http) =>
{
    AuthCookieWriter.Clear(http.Response);
    return Results.Ok();
})
.RequireAuthorization()
.WithName("Logout")
.WithSummary("Выход (удаление auth-cookie)");

// Явное продление сессии: перевыпуск JWT со свежей ролью из БД.
app.MapPost("/api/auth/refresh", async (
    ICurrentUserProvider me,
    IUserRepository users,
    JwtTokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    HttpContext http) =>
{
    var id = me.GetCurrentUserId();
    if (id <= 0)
        return Results.Unauthorized();

    var user = await users.GetByIdAsync(id);
    if (user is null)
        return Results.Unauthorized();

    AuthCookieWriter.Append(
        http.Response,
        tokens.CreateToken(user.Id, user.Role.ToString()),
        jwtOptions.Value.ExpiresDays,
        http.Request.IsHttps);

    return Results.Ok(new { userId = user.Id, role = user.Role.ToString() });
})
.RequireAuthorization()
.WithName("RefreshSession")
.WithSummary("Продление сессии (перевыпуск JWT-cookie)");

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
