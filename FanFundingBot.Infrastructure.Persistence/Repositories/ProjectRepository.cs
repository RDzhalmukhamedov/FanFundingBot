using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Infrastructure.Persistence.Repositories;

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
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project?> GetByIdWithAddonsAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.Addons)
            .Include(p => p.Status)
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Project>> GetAllAsync()
    {
        return await _context.Projects
            .Include(p => p.Status)
            .Include(p => p.Members)
            .ToListAsync();
    }

    public async Task<List<Project>> GetAllWithAddonsAsync()
    {
        return await _context.Projects
            .Include(p => p.Addons)
            .Include(p => p.Status)
            .Include(p => p.Members)
            .ToListAsync();
    }

    public async Task AddAsync(Project project)
    {
        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        var tracked = _context.ChangeTracker.Entries<Project>()
            .FirstOrDefault(e => e.Entity.Id == project.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        var existingAddonIds = await _context.Addons
            .Where(a => EF.Property<Guid>(a, "ProjectId") == project.Id)
            .Select(a => a.Id)
            .ToListAsync();

        var existingMemberIds = await _context.Set<ProjectMember>()
            .Where(m => m.ProjectId == project.Id)
            .Select(m => m.UserId)
            .ToListAsync();

        _context.Projects.Update(project);
        foreach (var addon in project.Addons)
        {
            if (!existingAddonIds.Contains(addon.Id))
                _context.Entry(addon).State = EntityState.Added;
        }
        foreach (var member in project.Members)
        {
            if (!existingMemberIds.Contains(member.UserId))
                _context.Entry(member).State = EntityState.Added;
        }

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
