using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace FanFundingBot.Application.Common.Errors;

/// <summary>
/// Фабрики типизированных ошибок. Сообщения 1:1 совпадают с историческими
/// строковыми литералами, поэтому Bot/Web, показывающие Errors[0].Message,
/// продолжают работать без изменений. Новый код может делать match по типу/коду.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ResultErrors
{
    public static Error ProjectNotFound() =>
        new NotFoundError("project.not_found", "Проект не найден.");

    public static Error UserNotFound() =>
        new NotFoundError("user.not_found", "Пользователь не найден.");

    public static Error OrderNotFound() =>
        new NotFoundError("order.not_found", "Заказ не найден.");

    public static Error AddonNotFound() =>
        new NotFoundError("addon.not_found", "Дополнение не найдено.");

    public static Error AddonNotFound(Guid addonId) =>
        new NotFoundError("addon.not_found", $"Дополнение {addonId} не найдено.");

    public static Error QuestionNotFound() =>
        new NotFoundError("question.not_found", "Вопрос не найден.");

    public static Error StatusNotFound() =>
        new NotFoundError("status.not_found", "Статус не найден.");

    public static Error BaseProjectStatusNotFound() =>
        new NotFoundError("status.not_found", "Базовый статус проекта не найден.");

    public static Error Forbidden() =>
        new ForbiddenError("auth.forbidden", "Нет прав.");

    public static Error DuplicateOrder() =>
        new ConflictError("order.duplicate", "У вас уже есть заказ на этот проект.");

    public static Error ProjectUnavailable() =>
        new ConflictError("project.unavailable", "Проект недоступен для заказа.");
}
