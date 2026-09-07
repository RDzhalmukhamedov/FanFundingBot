using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Domain.Interfaces;

public interface IUserRepository
{
    Task<Customer?> GetByIdAsync(long id);
    Task<List<Customer>> GetByRoleAsync(UserRole role);
    Task<List<Customer>> GetSubscribedToProjectAsync(Guid projectId);
    Task AddAsync(Customer user);
    Task UpdateAsync(Customer user);
    Task<bool> ExistsAsync(long id);
}
