namespace FanFundingBot.Application.Abstractions;

/// <summary>
/// Портовое исключение: не удалось подобрать свободный <c>ProjectStatus.Order</c>
/// за 3 попытки (конкурентные создания). Infrastructure транслирует в него
/// unique-violation БД (PG 23505), Application ловит и возвращает <c>ConflictError</c>.
/// Application никогда не видит EF/PG-типы напрямую (Dependency Rule).
/// </summary>
public sealed class ProjectStatusOrderConflictException : Exception
{
    public ProjectStatusOrderConflictException()
        : base("Не удалось создать статус: порядок занят. Попробуйте ещё раз.")
    {
    }
}
