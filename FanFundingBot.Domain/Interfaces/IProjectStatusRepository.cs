using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Domain.Interfaces;

public interface IProjectStatusRepository
{
    Task<List<ProjectStatus>> GetAllAsync();
    Task<int> GetMaxOrderAsync();
    Task<ProjectStatus?> GetByIdAsync(int id);
    Task AddAsync(ProjectStatus entry);
}
