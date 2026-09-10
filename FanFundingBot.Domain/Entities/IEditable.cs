using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Domain.Entities;

public interface IEditable<T> where T : IEditable<T>
{
    EditMode Mode { get; }

    T StartEdit();

    T FinishEdit();
}
