using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot;

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
        services.AddSingleton<IOrderProcessingQueue, OrderProcessingQueue>();

        services.AddScoped<IOrderFlowService>(sp =>
        {
            var orderRepo = sp.GetRequiredService<IOrderRepository>();
            var projectRepo = sp.GetRequiredService<IProjectRepository>();
            var addonRepo = sp.GetRequiredService<IAddonRepository>();
            var userRepo = sp.GetRequiredService<IUserRepository>();
            var externalStorage = sp.GetRequiredService<IOrderExternalStorage>();
            var botClient = sp.GetRequiredService<ITelegramBotClient>();
            var config = sp.GetRequiredService<BotConfiguration>();

            OwnerNotificationDelegate notify = async (message) =>
            {
                try
                {
                    await botClient.SendMessage(
                        chatId: config.OwnerId,
                        text: $"\u26A0\uFE0F <b>FanFundingBot: ошибка внешнего хранилища</b>\n\n{message}",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                }
                catch { }
            };

            return new OrderFlowService(orderRepo, projectRepo, addonRepo, userRepo, externalStorage, notify);
        });
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IUIRenderer, UIRenderer>();
        services.AddScoped<ITextInputHandler, TextInputHandler>();

        // F2: Text input handlers
        services.AddScoped<IQuestionTextHandler, QuestionTextHandler>();
        services.AddScoped<IBroadcastTextHandler, BroadcastTextHandler>();
        services.AddScoped<IOrderTextHandler, OrderTextHandler>();
        services.AddScoped<IAdminProjectCreationTextHandler, AdminProjectCreationTextHandler>();
        services.AddScoped<IAdminProjectEditingTextHandler, AdminProjectEditingTextHandler>();
        services.AddScoped<ICommandHandler, CommandHandler>();
        services.AddScoped<ICallbackQueryHandler, CallbackQueryHandler>();
        services.AddScoped<IUserCallbackQueryHandler, UserCallbackQueryHandler>();

        // F3: Admin callback handlers
        services.AddScoped<IAdminStatusCallbackHandler, AdminStatusCallbackHandler>();
        services.AddScoped<IAdminQuestionCallbackHandler, AdminQuestionCallbackHandler>();
        services.AddScoped<IAdminProjectCallbackHandler, AdminProjectCallbackHandler>();

        // F1: Admin command handler
        services.AddScoped<IAdminCommandHandler, AdminCommandHandler>();

        // F7: Project edit form builder
        services.AddScoped<IProjectEditFormBuilder, ProjectEditFormBuilder>();

        // F4: Callback command handlers for dictionary dispatch
        services.AddScoped<ICallbackCommandHandler, UserCallbackCommandHandler>();
        services.AddScoped<ICallbackCommandHandler, AdminCallbackCommandHandler>();

        services.AddScoped<IContactHandler, ContactHandler>();
        services.AddScoped<IUpdateHandler, UpdateHandler>();
        services.AddScoped<MainMenuHelper>();
        services.AddScoped<ProjectAccessHelper>();
        services.AddHostedService<SessionExpirationService>();

        return services;
    }
}