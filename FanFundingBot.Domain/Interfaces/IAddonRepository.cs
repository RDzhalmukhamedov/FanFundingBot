using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Domain.Interfaces;

public interface IAddonRepository
{
    Task<Addon?> GetByIdAsync(Guid id);
    Task<List<Addon>> GetByProjectIdAsync(Guid projectId);
    Task<List<Addon>> GetByProjectIdPagedAsync(Guid projectId, int page, int pageSize);
    Task<int> CountByProjectIdAsync(Guid projectId);
    Task AddAsync(Addon addon);
    Task UpdateAsync(Addon addon);
    Task DeleteAsync(Guid id);
    Task<bool> HasOrdersAsync(Guid id);
    Task<Guid?> GetProjectIdByAddonAsync(Guid addonId);
}
