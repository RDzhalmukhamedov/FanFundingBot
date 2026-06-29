using FanFundingBot.Core.Entities;

namespace FanFundingBot.Core.Interfaces;

public interface IProjectStatusRepository
{
    Task<List<ProjectStatusEntry>> GetAllAsync();
    Task<ProjectStatusEntry?> GetByIdAsync(int id);
    Task AddAsync(ProjectStatusEntry entry);
}
