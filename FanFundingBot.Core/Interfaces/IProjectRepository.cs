using FanFundingBot.Core.Entities;

namespace FanFundingBot.Core.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id);
    Task<Project?> GetByIdWithAddonsAsync(Guid id);
    Task<List<Project>> GetAllAsync();
    Task<List<Project>> GetAllWithAddonsAsync();
    Task AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task DeleteAsync(Guid id);
}
