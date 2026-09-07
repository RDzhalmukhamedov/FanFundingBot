using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;

namespace FanFundingBot.Domain.Entities;

public class ProjectMember
{
    public Guid ProjectId { get; private set; }
    public long UserId { get; private set; }
    public ProjectRole Role { get; private set; }

    private ProjectMember()
    {
    }

    public ProjectMember(long userId, ProjectRole role)
    {
        if (userId <= 0)
            throw new DomainException($"Некорректный идентификатор пользователя: {userId}.");

        UserId = userId;
        Role = role;
    }
}
