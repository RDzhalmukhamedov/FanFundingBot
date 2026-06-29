using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Data.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.Status)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project?> GetByIdWithAddonsAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.Addons)
            .Include(p => p.Status)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Project>> GetAllAsync()
    {
        return await _context.Projects
            .Include(p => p.Status)
            .ToListAsync();
    }

    public async Task<List<Project>> GetAllWithAddonsAsync()
    {
        return await _context.Projects
            .Include(p => p.Addons)
            .Include(p => p.Status)
            .ToListAsync();
    }

    public async Task AddAsync(Project project)
    {
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }
    }
}
