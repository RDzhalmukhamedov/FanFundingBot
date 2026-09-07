using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Projects.Commands;

public sealed record CreateProjectCommand(
    long CallerUserId,
    string Name,
    string Description,
    List<NewAddonItem>? Addons) : IRequest<Result<Guid>>, IRequireGlobalAdmin;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Addons)
            .Must(items => items == null || items.All(ValidAddonItem))
            .WithMessage("Некорректное дополнение.");
    }

    private static bool ValidAddonItem(NewAddonItem item)
    {
        return !string.IsNullOrWhiteSpace(item.Name)
            && item.Name.Length <= 256
            && (item.Description ?? string.Empty).Length <= 2048
            && item.Price >= 0;
    }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Result<Guid>>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectStatusRepository _statuses;

    public CreateProjectCommandHandler(IProjectRepository projects, IProjectStatusRepository statuses)
    {
        _projects = projects;
        _statuses = statuses;
    }

    public async Task<Result<Guid>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var planned = await _statuses.GetByIdAsync(Domain.Constants.KnownProjectStatusIds.Planned);
        if (planned is null)
            return Result.Fail<Guid>("Базовый статус проекта не найден.");

        var project = new Project();
        project.StartEdit();
        project.SetName(request.Name);
        project.SetDescription(request.Description);
        project.SetStatus(planned);

        if (request.Addons is not null)
        {
            foreach (var item in request.Addons)
            {
                var addon = new Addon();
                addon.SetName(item.Name);
                addon.SetDescription(item.Description);
                addon.SetPrice(item.Price);
                addon.FinishEdit();
                project.AddAddon(addon);
            }
        }

        project.FinishEdit();
        project.AddMember(request.CallerUserId, Domain.Enums.ProjectRole.ProjectAdmin);

        await _projects.AddAsync(project);

        return Result.Ok(project.Id);
    }
}
