using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Questions.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Questions.Queries;

public sealed record GetUnansweredQuestionsQuery(long CallerUserId)
    : IRequest<Result<List<QuestionDto>>>, IRequireGlobalAdmin;

public class GetUnansweredQuestionsQueryHandler
    : IRequestHandler<GetUnansweredQuestionsQuery, Result<List<QuestionDto>>>
{
    private readonly IQuestionRepository _questions;

    public GetUnansweredQuestionsQueryHandler(IQuestionRepository questions)
    {
        _questions = questions;
    }

    public async Task<Result<List<QuestionDto>>> Handle(
        GetUnansweredQuestionsQuery request, CancellationToken cancellationToken)
    {
        var questions = await _questions.GetUnansweredAsync();
        return Result.Ok(questions.Adapt<List<QuestionDto>>());
    }
}
