using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Questions.Commands;

public sealed record AskQuestionCommand(long CallerUserId, string Text) : IRequest<Result<Guid>>;

public class AskQuestionCommandValidator : AbstractValidator<AskQuestionCommand>
{
    public AskQuestionCommandValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public class AskQuestionCommandHandler : IRequestHandler<AskQuestionCommand, Result<Guid>>
{
    private readonly IUserRepository _users;
    private readonly IQuestionRepository _questions;

    public AskQuestionCommandHandler(IUserRepository users, IQuestionRepository questions)
    {
        _users = users;
        _questions = questions;
    }

    public async Task<Result<Guid>> Handle(AskQuestionCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.CallerUserId);
        if (user is null)
            return Result.Fail<Guid>(ResultErrors.UserNotFound());

        var question = new Question(user, request.Text);
        await _questions.AddAsync(question);

        return Result.Ok(question.Id);
    }
}

public sealed record AnswerQuestionCommand(long CallerUserId, Guid QuestionId, string Answer)
    : IRequest<Result>, IRequireGlobalAdmin;

public class AnswerQuestionCommandValidator : AbstractValidator<AnswerQuestionCommand>
{
    public AnswerQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(4000);
    }
}

public class AnswerQuestionCommandHandler : IRequestHandler<AnswerQuestionCommand, Result>
{
    private readonly IUserRepository _users;
    private readonly IQuestionRepository _questions;
    private readonly Abstractions.INotificationPort _notifications;

    public AnswerQuestionCommandHandler(
        IUserRepository users,
        IQuestionRepository questions,
        Abstractions.INotificationPort notifications)
    {
        _users = users;
        _questions = questions;
        _notifications = notifications;
    }

    public async Task<Result> Handle(AnswerQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _questions.GetByIdAsync(request.QuestionId);
        if (question is null)
            return Result.Fail(ResultErrors.QuestionNotFound());

        var admin = await _users.GetByIdAsync(request.CallerUserId);
        if (admin is null || !admin.IsAdmin())
            return Result.Fail(ResultErrors.Forbidden());

        question.AnswerQuestion(admin, request.Answer);
        await _questions.UpdateAsync(question);

        await _notifications.NotifyQuestionAnsweredAsync(
            question.User.Id, question.Text, request.Answer, cancellationToken);

        return Result.Ok();
    }
}
