using FanFundingBot.App.Components;
using FanFundingBot.App.Services;
using FanFundingBot.Application.Abstractions;
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
