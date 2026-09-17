using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Domain.Common;

public interface IEditable<T> where T : IEditable<T>
{
    EditMode Mode { get; }

    T StartEdit();

    T FinishEdit();
}
