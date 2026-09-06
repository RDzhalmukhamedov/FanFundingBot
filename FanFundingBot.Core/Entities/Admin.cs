using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public class Admin : Customer
{
    public override UserRole Role => UserRole.Admin;

    public Admin(string firstName, string lastName, string userName) : base(firstName, lastName, userName)
    {
    }
}
