namespace FanFundingBot.Domain.Enums;

public enum OrderFlowState
{
    Idle,
    SelectingAddons,
    EnteringPhone,
    SelectingDelivery,
    EnteringAddress,
    Confirming
}
