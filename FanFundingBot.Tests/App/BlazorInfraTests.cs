using FanFundingBot.App.Components;
using FanFundingBot.App.Components.Shared;
using FanFundingBot.App.Services;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Infrastructure.ExternalStorage;
using FluentAssertions;
using FluentResults;
using NSubstitute;

namespace FanFundingBot.Tests.App;

[TestFixture]
public class BlazorResultTests
{
    [Test]
    public void Message_Success_ReturnsNull()
    {
        BlazorResult.Message(Result.Ok()).Should().BeNull();
    }

    [Test]
    public void Message_Failure_ReturnsFirstErrorMessage()
    {
        BlazorResult.Message(Result.Fail("бум")).Should().Be("бум");
    }
}

[TestFixture]
public class OrderStatusTextsTests
{
    [TestCase(OrderStatus.New, "🆕 Новый")]
    [TestCase(OrderStatus.Confirmed, "✅ Подтверждён")]
    [TestCase(OrderStatus.Completed, "✔️ Выполнен")]
    [TestCase(OrderStatus.Cancelled, "❌ Отменён")]
    public void Text_KnownStatus_ReturnsBadge(OrderStatus status, string expected)
    {
        OrderStatusTexts.Text(status).Should().Be(expected);
    }
}

[TestFixture]
public class ProjectFormsTests
{
    private static AddonForm ValidAddon() => new()
    {
        Name = "Доп",
        Description = "Описание",
        Price = 10
    };

    [Test]
    public void ValidateAddons_Valid_ReturnsNull()
    {
        ProjectForms.ValidateAddons([ValidAddon()]).Should().BeNull();
    }

    [Test]
    public void ValidateAddons_EmptyName_ReturnsError()
    {
        var addon = ValidAddon();
        addon.Name = "  ";

        ProjectForms.ValidateAddons([addon]).Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidateAddons_LongName_ReturnsError()
    {
        var addon = ValidAddon();
        addon.Name = new string('x', 257);

        ProjectForms.ValidateAddons([addon]).Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidateAddons_NegativePrice_ReturnsError()
    {
        var addon = ValidAddon();
        addon.Price = -1;

        ProjectForms.ValidateAddons([addon]).Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ValidateAddons_FirstErrorWins()
    {
        var bad = ValidAddon();
        bad.Name = string.Empty;
        var alsoBad = ValidAddon();
        alsoBad.Price = -5;

        ProjectForms.ValidateAddons([bad, alsoBad]).Should().Contain("название");
    }
}

[TestFixture]
public class ProjectSheetLinkServiceTests
{
    private const string SpreadsheetId = "abc123";

    private static ProjectSheetLinkService Create(
        bool canManage,
        out ICurrentUserProvider me,
        long userId = 7)
    {
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.CanManageProjectAsync(userId, Arg.Any<Guid>()).Returns(canManage);
        me = Substitute.For<ICurrentUserProvider>();
        me.GetCurrentUserId().Returns(userId);
        return new ProjectSheetLinkService(
            authorization, me, new GoogleSheetsOptions { SpreadsheetId = SpreadsheetId });
    }

    [Test]
    public async Task GetUrlAsync_Allowed_ReturnsDocsUrl()
    {
        var service = Create(canManage: true, out _);
        var projectId = Guid.NewGuid();

        var result = await service.GetUrlAsync(projectId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be($"https://docs.google.com/spreadsheets/d/{SpreadsheetId}");
    }

    [Test]
    public async Task GetUrlAsync_Forbidden_Fails()
    {
        var service = Create(canManage: false, out _);

        var result = await service.GetUrlAsync(Guid.NewGuid());

        result.IsFailed.Should().BeTrue();
    }
}
