using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Models;
using FanFundingBot.Application.Questions.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Questions.Queries;

public sealed record GetUnansweredQuestionsQuery(long CallerUserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<QuestionDto>>>, IRequireGlobalAdmin;

public class GetUnansweredQuestionsQueryHandler
    : IRequestHandler<GetUnansweredQuestionsQuery, Result<PagedResult<QuestionDto>>>
{
    private readonly IQuestionRepository _questions;

    public GetUnansweredQuestionsQueryHandler(IQuestionRepository questions)
    {
        _questions = questions;
    }

    public async Task<Result<PagedResult<QuestionDto>>> Handle(
        GetUnansweredQuestionsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await _questions.GetUnansweredPagedAsync(page, pageSize);
        return Result.Ok(new PagedResult<QuestionDto>(items.Adapt<List<QuestionDto>>(), total, page, pageSize));
    }
}
