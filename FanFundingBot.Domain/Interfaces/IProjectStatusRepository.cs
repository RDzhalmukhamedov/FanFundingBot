using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Domain.Interfaces;

public interface IProjectStatusRepository
{
    Task<List<ProjectStatus>> GetAllAsync();
    Task<ProjectStatus?> GetByIdAsync(int id);
    Task AddAsync(ProjectStatus entry);
}
