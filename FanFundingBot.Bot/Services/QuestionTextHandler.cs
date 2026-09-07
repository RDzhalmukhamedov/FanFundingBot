using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Services;

public class QuestionTextHandler : IQuestionTextHandler
{
    private readonly IUserStateService _stateService;
    private readonly IQuestionRepository _questionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminService _adminService;

    public QuestionTextHandler(
        IUserStateService stateService,
        IQuestionRepository questionRepository,
        IUserRepository userRepository,
        IAdminService adminService)
    {
        _stateService = stateService;
        _questionRepository = questionRepository;
        _userRepository = userRepository;
        _adminService = adminService;
    }

    public async Task SaveQuestionAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text!;

        var customer = await _userRepository.GetByIdAsync(userId);
        if (customer == null)
        {
            customer = new Domain.Entities.User(userId, message.From.FirstName, message.From.LastName, message.From.Username);
            await _userRepository.AddAsync(customer);
        }

        var question = new Question(customer, text);

        await _questionRepository.AddAsync(question);

        var session = _stateService.Get(userId);
        if (session != null)
        {
            session.PendingAction = null;
            _stateService.Update(userId, session);
        }

        await botClient.SendMessage(
            message.Chat.Id,
            "✅ Ваш вопрос принят! Мы ответим вам в ближайшее время.",
            replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(userId)),
            cancellationToken: ct);
    }

    public async Task SaveAnswerAsync(ITelegramBotClient botClient, Message message, Guid questionId, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text!;

        var result = await _adminService.AnswerQuestionAsync(questionId, text, userId);

        var session = _stateService.Get(userId);
        if (session != null)
        {
            session.PendingAction = null;
            session.PendingQuestionId = null;
            _stateService.Update(userId, session);
        }

        await botClient.SendMessage(
            message.Chat.Id,
            result,
            replyMarkup: MainMenuKeyboard.Create(true),
            cancellationToken: ct);
    }
}