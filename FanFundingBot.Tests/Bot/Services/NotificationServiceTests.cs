using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FluentAssertions;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class NotificationServiceTests
{
    private ITelegramBotClient _botClient;
    private NotificationService _service;

    [SetUp]
    public void Setup()
    {
        _botClient = Substitute.For<ITelegramBotClient>();
        _service = new NotificationService(_botClient, new BotConfiguration());
    }

    [Test]
    public async Task NotifyQuestionAnswered_ShouldSendAnswer()
    {
        await _service.NotifyQuestionAnsweredAsync(1, "Q?", "Because.", CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text != null && r.Text.Contains("Because.")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NotifyProjectStatusChanged_ShouldSendToAll()
    {
        await _service.NotifyProjectStatusChangedAsync(
            new List<long> { 1, 2 }, "P", "Печать", CancellationToken.None);

        await _botClient.Received(2).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text != null && r.Text.Contains("Печать")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BroadcastToProject_ShouldSendText()
    {
        await _service.BroadcastToProjectAsync(
            new List<long> { 1 }, "Hello", CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text == "Hello"),
            Arg.Any<CancellationToken>());
    }
}
