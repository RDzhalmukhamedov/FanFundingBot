using FanFundingBot.Core.Entities;

namespace FanFundingBot.Core.Interfaces;

public interface IQuestionRepository
{
    Task<Question?> GetByIdAsync(Guid id);
    Task<List<Question>> GetByUserAsync(long userId);
    Task<List<Question>> GetUnansweredAsync();
    Task<List<Question>> GetAllAsync();
    Task AddAsync(Question question);
    Task UpdateAsync(Question question);
}
