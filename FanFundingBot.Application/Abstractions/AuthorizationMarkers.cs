namespace FanFundingBot.Application.Abstractions;

public interface IRequireGlobalAdmin
{
}

public interface IRequireProjectAccess
{
    Guid ProjectId { get; }
}

public interface IRequireAddonAccess
{
    Guid AddonId { get; }
}
