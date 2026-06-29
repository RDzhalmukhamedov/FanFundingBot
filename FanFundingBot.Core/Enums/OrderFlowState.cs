namespace FanFundingBot.Core.Enums;

public enum OrderFlowState
{
    Idle,
    SelectingAddons,
    EnteringPhone,
    SelectingDelivery,
    EnteringAddress,
    Confirming
}
