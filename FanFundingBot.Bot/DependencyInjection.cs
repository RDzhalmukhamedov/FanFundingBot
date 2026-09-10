using FanFundingBot.Application.Abstractions;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace FanFundingBot.Bot;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddBotLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var botConfig = new BotConfiguration();
        configuration.Bind("BotConfiguration", botConfig);
        services.AddSingleton(botConfig);

        services.AddMemoryCache();
        services.AddSingleton<IUserStateService, UserStateService>();

        services.AddScoped<TelegramUserContext>();
        services.AddScoped<ICurrentUserProvider, TelegramCurrentUserProvider>();
        services.AddScoped<INotificationPort, NotificationService>();

        services.AddScoped<IUIRenderer, UIRenderer>();
        services.AddScoped<ITextInputHandler, TextInputHandler>();

        services.AddScoped<IQuestionTextHandler, QuestionTextHandler>();
        services.AddScoped<IBroadcastTextHandler, BroadcastTextHandler>();
        services.AddScoped<IOrderTextHandler, OrderTextHandler>();
        services.AddScoped<IAdminProjectCreationTextHandler, AdminProjectCreationTextHandler>();
        services.AddScoped<IAdminProjectEditingTextHandler, AdminProjectEditingTextHandler>();
        services.AddScoped<ICommandHandler, CommandHandler>();
        services.AddScoped<ICallbackQueryHandler, CallbackQueryHandler>();
        services.AddScoped<IUserCallbackQueryHandler, UserCallbackQueryHandler>();

        services.AddScoped<IAdminStatusCallbackHandler, AdminStatusCallbackHandler>();
        services.AddScoped<IAdminQuestionCallbackHandler, AdminQuestionCallbackHandler>();
        services.AddScoped<IAdminProjectCallbackHandler, AdminProjectCallbackHandler>();

        services.AddScoped<IAdminCommandHandler, AdminCommandHandler>();

        services.AddScoped<IProjectEditFormBuilder, ProjectEditFormBuilder>();

        services.AddScoped<ICallbackCommandHandler, UserCallbackCommandHandler>();
        services.AddScoped<ICallbackCommandHandler, AdminCallbackCommandHandler>();

        services.AddScoped<IContactHandler, ContactHandler>();
        services.AddScoped<IUpdateHandler, UpdateHandler>();
        services.AddScoped<MainMenuHelper>();
        services.AddHostedService<SessionExpirationService>();

        return services;
    }
}
