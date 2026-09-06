using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Interfaces;

public interface IUserRepository
{
    Task<Customer?> GetByIdAsync(long id);
    Task<List<Customer>> GetByRoleAsync(UserRole role);
    Task<List<Customer>> GetSubscribedToProjectAsync(Guid projectId);
    Task AddAsync(Customer user);
    Task UpdateAsync(Customer user);
    Task<bool> ExistsAsync(long id);
}
