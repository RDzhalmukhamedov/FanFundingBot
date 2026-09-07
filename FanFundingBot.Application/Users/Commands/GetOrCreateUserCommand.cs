using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Users.Commands;

public sealed record GetOrCreateUserCommand(
    long UserId,
    string? FirstName,
    string? LastName,
    string? Username,
    bool IsOwner) : IRequest<Result>;

public class GetOrCreateUserCommandValidator : AbstractValidator<GetOrCreateUserCommand>
{
    public GetOrCreateUserCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}

public class GetOrCreateUserCommandHandler : IRequestHandler<GetOrCreateUserCommand, Result>
{
    private readonly IUserRepository _users;

    public GetOrCreateUserCommandHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<Result> Handle(GetOrCreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId);
        if (user is not null)
        {
            if (request.IsOwner && !user.IsAdmin())
            {
                user.PromoteToAdmin();
                await _users.UpdateAsync(user);
            }

            return Result.Ok();
        }

        var created = new Domain.Entities.User(
            request.UserId, request.FirstName, request.LastName, request.Username);
        if (request.IsOwner)
            created.PromoteToAdmin();

        await _users.AddAsync(created);

        return Result.Ok();
    }
}
