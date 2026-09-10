using FanFundingBot.Application.Abstractions;
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
            response.Reasons.Add(new Error(ex.Message));
            return response;
        }
    }
}
