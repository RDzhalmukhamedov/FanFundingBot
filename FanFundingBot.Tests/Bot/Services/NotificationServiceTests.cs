using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
        _service = new NotificationService(_botClient, new BotConfiguration(), NullLogger<NotificationService>.Instance);
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

    [Test]
    public async Task NotifyOrderStatusChanged_ShouldSendStatus()
    {
        await _service.NotifyOrderStatusChangedAsync(
            7, "P", Domain.Enums.OrderStatus.Confirmed, CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text != null && r.Text.Contains("Подтверждён")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendDirectMessage_ShouldSendText()
    {
        await _service.SendDirectMessageAsync(7, "Hello", CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text == "Hello"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NotifyQuestionAnswered_ShouldEscapeHtmlInUserText()
    {
        await _service.NotifyQuestionAnsweredAsync(1, "<b>Q</b>", "Ответ <i>тут</i>", CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null
                && r.Text.Contains("&lt;b&gt;Q&lt;/b&gt;")
                && r.Text.Contains("Ответ &lt;i&gt;тут&lt;/i&gt;")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BroadcastToProject_ShouldEscapeHtml()
    {
        await _service.BroadcastToProjectAsync(
            new List<long> { 1 }, "Скидка <10%", CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text == "Скидка &lt;10%"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendDirectMessage_ShouldEscapeHtml()
    {
        await _service.SendDirectMessageAsync(7, "a<b>c", CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text == "a&lt;b&gt;c"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NotifyProjectStatusChanged_ShouldEscapeProjectName()
    {
        await _service.NotifyProjectStatusChangedAsync(
            new List<long> { 1 }, "<Проект>", "Печать", CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text != null && r.Text.Contains("&lt;Проект&gt;")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NotifyOrderStatusChanged_ShouldEscapeProjectName()
    {
        await _service.NotifyOrderStatusChangedAsync(
            7, "<P>", Domain.Enums.OrderStatus.Confirmed, CancellationToken.None);

        await _botClient.Received(1).SendRequest<Telegram.Bot.Types.Message>(
            Arg.Is<SendMessageRequest>(r => r.Text != null && r.Text.Contains("&lt;P&gt;")),
            Arg.Any<CancellationToken>());
    }
}
