using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Projects.Commands;

public sealed record DeleteProjectCommand(long CallerUserId, Guid ProjectId)
    : IRequest<Result>, IRequireProjectAccess;

public class DeleteProjectCommandValidator : AbstractValidator<DeleteProjectCommand>
{
    public DeleteProjectCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
    }
}

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Result>
{
    private readonly IProjectRepository _projects;
    private readonly IOrderRepository _orders;

    public DeleteProjectCommandHandler(IProjectRepository projects, IOrderRepository orders)
    {
        _projects = projects;
        _orders = orders;
    }

    public async Task<Result> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId);
        if (project is null)
            return Result.Fail(ResultErrors.ProjectNotFound());

        var orders = await _orders.GetByProjectAsync(request.ProjectId);
        if (orders.Count > 0)
            return Result.Fail(ResultErrors.ProjectHasOrders());

        await _projects.DeleteAsync(request.ProjectId);

        return Result.Ok();
    }
}
