using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Domain.Interfaces;

public interface IQuestionRepository
{
    Task<Question?> GetByIdAsync(Guid id);
    Task<List<Question>> GetByUserAsync(long userId);
    Task<List<Question>> GetUnansweredAsync();
    Task<(List<Question> Items, int TotalCount)> GetUnansweredPagedAsync(int page, int pageSize);
    Task<List<Question>> GetAllAsync();
    Task AddAsync(Question question);
    Task UpdateAsync(Question question);
}
