using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Domain.Exceptions;
using FluentResults;

namespace FanFundingBot.Application.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
    where TResponse : ResultBase, new()
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _unitOfWork.ExecuteAsync(() => next(), cancellationToken);
        }
        catch (DomainException ex)
        {
            var response = new TResponse();
            response.Reasons.Add(MapDomainException(ex));
            return response;
        }
    }

    private static Error MapDomainException(DomainException ex)
    {
        return ex.Code switch
        {
            "order.not_found" or "addon.not_found" or "project.not_found"
                or "user.not_found" or "question.not_found" or "status.not_found"
                or "project.member.not_found"
                => new NotFoundError(ex.Code, ex.Message),
            "auth.forbidden" => new ForbiddenError(ex.Code, ex.Message),
            "order.duplicate" or "project.has_orders" or "addon.duplicate"
                or "project.member.duplicate" or "project.unavailable" or "project.limit"
                or "order.not_editable" or "project.not_editable" or "addon.not_editable"
                or "edit.conflict"
                => new ConflictError(ex.Code, ex.Message),
            _ when ex is OrderNotEditModeException or ProjectNotEditModeException or AddonNotEditModeException
                => new ConflictError("edit.conflict", ex.Message),
            _ => new Error(ex.Message),
        };
    }
}
