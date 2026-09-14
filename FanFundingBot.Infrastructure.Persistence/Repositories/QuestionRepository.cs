using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.Infrastructure.Persistence.Repositories;

public class QuestionRepository : IQuestionRepository
{
    private readonly AppDbContext _context;

    public QuestionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Question?> GetByIdAsync(Guid id)
    {
        return await _context.Questions
            .Include(q => q.User)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<List<Question>> GetByUserAsync(long userId)
    {
        return await _context.Questions
            .Where(q => q.User.Id == userId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Question>> GetUnansweredAsync()
    {
        return await _context.Questions
            .Include(q => q.User)
            .Where(q => q.Answer == null)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<Question> Items, int TotalCount)> GetUnansweredPagedAsync(int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Questions.Where(q => q.Answer == null);
        var total = await query.CountAsync();

        var items = await query
            .Include(q => q.User)
            .OrderBy(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<Question>> GetAllAsync()
    {
        return await _context.Questions
            .Include(q => q.User)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Question question)
    {
        await _context.Questions.AddAsync(question);
    }

    public Task UpdateAsync(Question question)
    {
        _context.Questions.Update(question);
        return Task.CompletedTask;
    }
}
