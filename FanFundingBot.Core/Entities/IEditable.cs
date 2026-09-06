using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public interface IEditable<T> where T : IEditable<T>
{
    EditMode Mode { get; }

    T StartEdit();

    T FinishEdit();
}
