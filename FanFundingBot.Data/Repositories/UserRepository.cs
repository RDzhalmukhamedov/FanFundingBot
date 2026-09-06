using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(long id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<List<Customer>> GetByRoleAsync(UserRole role)
    {
        return await _context.Users
            .Where(u => u.Role == role)
            .ToListAsync();
    }

    public async Task<List<Customer>> GetSubscribedToProjectAsync(Guid projectId)
    {
        return await _context.Orders
            .Where(o => o.ProjectId == projectId && o.Status != OrderStatus.Cancelled)
            .Select(o => o.User)
            .Distinct()
            .ToListAsync();
    }

    public async Task AddAsync(Customer user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Customer user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(long id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id);
    }
}
