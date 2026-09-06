using FanFundingBot.Core.Interfaces;

namespace FanFundingBot.Bot.Helpers;

public class ProjectAccessHelper
{
    private readonly IProjectRepository _projectRepository;

    public ProjectAccessHelper(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<(bool Allowed, string? ErrorMessage)> CanEditOrdersAsync(Guid projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return (false, "Проект не найден.");
        if (project.Status?.CanEditOrders != true)
            return (false, "Редактирование заказа недоступно для текущего статуса проекта.");
        return (true, null);
    }
}
