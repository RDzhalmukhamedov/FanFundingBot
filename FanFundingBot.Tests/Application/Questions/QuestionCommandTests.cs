using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Questions.Commands;
using FanFundingBot.Application.Questions.Queries;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace FanFundingBot.Tests.Application.Questions;

[TestFixture]
public class QuestionCommandTests
{
    private IUserRepository _users;
    private IQuestionRepository _questions;
    private INotificationPort _notifications;

    [SetUp]
    public void Setup()
    {
        _users = Substitute.For<IUserRepository>();
        _questions = Substitute.For<IQuestionRepository>();
        _notifications = Substitute.For<INotificationPort>();
    }

    [Test]
    public async Task AskQuestion_Happy_ShouldSave()
    {
        _users.GetByIdAsync(1).Returns(DomainTestFactory.NewCustomer(1));

        Question? saved = null;
        await _questions.AddAsync(Arg.Do<Question>(q => saved = q));

        var handler = new AskQuestionCommandHandler(_users, _questions);
        var result = await handler.Handle(
            new AskQuestionCommand(1, "How much?"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Text.Should().Be("How much?");
    }

    [Test]
    public async Task AskQuestion_UserNotFound_ShouldFail()
    {
        _users.GetByIdAsync(Arg.Any<long>()).Returns((User?)null);

        var handler = new AskQuestionCommandHandler(_users, _questions);
        var result = await handler.Handle(
            new AskQuestionCommand(999, "Hi"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _questions.DidNotReceive().AddAsync(Arg.Any<Question>());
    }

    [Test]
    public async Task AnswerQuestion_Happy_ShouldAnswerAndNotify()
    {
        var asker = DomainTestFactory.NewCustomer(2);
        var question = new Question(asker, "Q?");
        var admin = DomainTestFactory.NewAdmin(1);
        _questions.GetByIdAsync(question.Id).Returns(question);
        _users.GetByIdAsync(1).Returns(admin);

        var handler = new AnswerQuestionCommandHandler(_users, _questions, _notifications);
        var result = await handler.Handle(
            new AnswerQuestionCommand(1, question.Id, "Because."), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        question.Answer.Should().Be("Because.");
        await _questions.Received(1).UpdateAsync(question);
        await _notifications.Received(1).NotifyQuestionAnsweredAsync(
            2, "Q?", "Because.", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AnswerQuestion_NotFound_ShouldFail()
    {
        _questions.GetByIdAsync(Arg.Any<Guid>()).Returns((Question?)null);

        var handler = new AnswerQuestionCommandHandler(_users, _questions, _notifications);
        var result = await handler.Handle(
            new AnswerQuestionCommand(1, Guid.NewGuid(), "A"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task AnswerQuestion_NonAdmin_ShouldFail()
    {
        var question = new Question(DomainTestFactory.NewCustomer(2), "Q?");
        _questions.GetByIdAsync(question.Id).Returns(question);
        _users.GetByIdAsync(3).Returns(DomainTestFactory.NewCustomer(3));

        var handler = new AnswerQuestionCommandHandler(_users, _questions, _notifications);
        var result = await handler.Handle(
            new AnswerQuestionCommand(3, question.Id, "A"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _questions.DidNotReceive().UpdateAsync(Arg.Any<Question>());
    }

    [Test]
    public async Task GetUnanswered_ShouldMapDtos()
    {
        var question = new Question(DomainTestFactory.NewCustomer(2, "asker"), "Q?");
        _questions.GetUnansweredAsync().Returns(new List<Question> { question });

        var handler = new GetUnansweredQuestionsQueryHandler(_questions);
        var result = await handler.Handle(
            new GetUnansweredQuestionsQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Should().ContainSingle().Subject;
        dto.Username.Should().Be("asker");
        dto.Text.Should().Be("Q?");
    }
}
