using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Enums;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class UIRendererTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    private ITelegramBotClient _botClient;
    private IMediator _mediator;
    private UIRenderer _renderer;

    [SetUp]
    public void Setup()
    {
        _botClient = Substitute.For<ITelegramBotClient>();
        _mediator = Substitute.For<IMediator>();
        _mediator.Send(Arg.Any<GetProjectByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new ProjectDto(
                ProjectId, "P", null, 1, "S", true, null,
                new List<AddonDto>(),
                new List<ProjectMemberDto>())));
        _renderer = new UIRenderer(Substitute.For<IUserStateService>(), _mediator, NullLogger<UIRenderer>.Instance);
    }

    private static OrderDto TestOrder(DeliveryMethod method, string? address, DateTime createdAt)
    {
        return new OrderDto(
            Guid.NewGuid(), 1, ProjectId, "P", "+79991234567",
            method, address, OrderStatus.New, createdAt,
            new List<OrderPositionDto> { new(Guid.NewGuid(), "A", 10m, 2) },
            20m);
    }

    [Test]
    public async Task MyOrder_PickupWithAddress_DoesNotShowAddress()
    {
        var order = TestOrder(
            DeliveryMethod.Pickup, "ул. Ленина, 1",
            new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc));

        await _renderer.HandleMyOrderByOrderAsync(_botClient, 200, order, CancellationToken.None);

        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null && !r.Text.Contains("Адрес")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MyOrder_DeliveryWithAddress_ShowsAddress()
    {
        var order = TestOrder(
            DeliveryMethod.Delivery, "ул. Ленина, 1",
            new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc));

        await _renderer.HandleMyOrderByOrderAsync(_botClient, 200, order, CancellationToken.None);

        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null && r.Text.Contains("Адрес: ул. Ленина, 1")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MyOrder_DateIsShownInMoscowTimeZone()
    {
        var order = TestOrder(
            DeliveryMethod.Delivery, "ул. Ленина, 1",
            new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc));

        await _renderer.HandleMyOrderByOrderAsync(_botClient, 200, order, CancellationToken.None);

        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null && r.Text.Contains("07.09.2026 15:00 (МСК)")),
            Arg.Any<CancellationToken>());
    }
}
