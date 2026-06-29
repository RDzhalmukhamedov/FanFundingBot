using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id);
    Task<List<User>> GetByRoleAsync(UserRole role);
    Task<List<User>> GetSubscribedToProjectAsync(Guid projectId);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> ExistsAsync(long id);
}
