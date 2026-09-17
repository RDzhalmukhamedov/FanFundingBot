using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Infrastructure.ExternalStorage;
using FluentResults;

namespace FanFundingBot.App.Services;

/// <summary>
/// Ссылка на Google-таблицу заказов: проверка <c>CanManage</c> + глобальный
/// <c>SpreadsheetId</c> (задел под per-project из PLAN_MULTI.md).
/// Используется Blazor напрямую.
/// </summary>
public sealed class ProjectSheetLinkService
{
    private readonly IAuthorizationService _authorization;
    private readonly ICurrentUserProvider _me;
    private readonly GoogleSheetsOptions _sheets;

    public ProjectSheetLinkService(
        IAuthorizationService authorization,
        ICurrentUserProvider me,
        GoogleSheetsOptions sheets)
    {
        _authorization = authorization;
        _me = me;
        _sheets = sheets;
    }

    public async Task<Result<string>> GetUrlAsync(Guid projectId)
    {
        if (!await _authorization.CanManageProjectAsync(_me.GetCurrentUserId(), projectId))
            return Result.Fail<string>(ResultErrors.Forbidden());

        if (string.IsNullOrWhiteSpace(_sheets.SpreadsheetId))
            return Result.Fail<string>(new NotFoundError("sheet.not_found", "Ссылка на таблицу не настроена."));

        return Result.Ok($"https://docs.google.com/spreadsheets/d/{_sheets.SpreadsheetId}");
    }
}
