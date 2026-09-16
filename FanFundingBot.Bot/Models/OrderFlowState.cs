namespace FanFundingBot.Bot.Models;

public enum OrderFlowState
{
    Idle,
    SelectingAddons,
    EnteringPhone,
    SelectingDelivery,
    EnteringAddress,
    Confirming
}
