using FanFundingBot.Core.Entities;

namespace FanFundingBot.Core.Interfaces;

public interface IProjectStatusRepository
{
    Task<List<ProjectStatus>> GetAllAsync();
    Task<ProjectStatus?> GetByIdAsync(int id);
    Task AddAsync(ProjectStatus entry);
}
