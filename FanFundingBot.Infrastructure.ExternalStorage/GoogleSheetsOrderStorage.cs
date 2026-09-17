using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;

namespace FanFundingBot.Infrastructure.ExternalStorage;

public class GoogleSheetsOrderStorage : IOrderExternalStorage
{
    private readonly ILogger<GoogleSheetsOrderStorage> _logger;
    private readonly GoogleSheetsOptions _options;
    private SheetsService? _sheetsService;
    private bool _initialized;
    private readonly object _initLock = new();

    private const string OrdersSheet = "Orders";
    private const string OrdersHistorySheet = "OrdersHistory";
    private const string AddonsSheet = "Addons";

    private const int ColOrderId = 0;
    private const int ColTelegramNick = 1;
    private const int ColUserName = 2;
    private const int ColPhone = 3;
    private const int ColProject = 4;
    private const int ColDelivery = 5;
    private const int ColAddress = 6;
    private const int ColStatus = 7;
    private const int ColCreatedAt = 8;
    private const int ColTotalCost = 9;
    private const int ColAddons = 10;
    private const int ColAddonIds = 11;
    private const int ColAction = 12;
    private const int ColChangedAt = 13;

    private static readonly string[] OrdersHeaders =
        ["Order ID", "Telegram Nick", "User Name", "Phone", "Project", "Delivery", "Address", "Status", "Created At", "Total Cost", "Addons", "Addon IDs"];

    private static readonly string[] HistoryHeaders =
        ["Order ID", "Telegram Nick", "User Name", "Phone", "Project", "Delivery", "Address", "Status", "Created At", "Total Cost", "Addons", "Addon IDs", "Action", "Changed At"];

    private static readonly string[] AddonsHeaders =
        ["Addon Name", "Addon ID", "Unit Price", "Total Quantity"];

    public GoogleSheetsOrderStorage(GoogleSheetsOptions options, ILogger<GoogleSheetsOrderStorage> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SaveOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeSheetsAsync(cancellationToken);

            var service = GetSheetsService();
            var rowData = OrderToRow(order);
            var action = "Created";

            var orderIdStr = order.Id.ToString();
            var (existingRows, existingRowIndex) = await FindOrderRowAsync(service, OrdersSheet, orderIdStr, cancellationToken);

            if (existingRowIndex.HasValue)
            {
                action = "Updated";
                var updateRange = $"{OrdersSheet}!A{existingRowIndex.Value}:{ColToLetter(ColAddonIds)}{existingRowIndex.Value}";
                var updateValue = new ValueRange { Values = new List<IList<object>> { rowData } };
                var updateRequest = service.Spreadsheets.Values.Update(updateValue, _options.SpreadsheetId, updateRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await updateRequest.ExecuteAsync(cancellationToken);
            }
            else
            {
                var appendValue = new ValueRange { Values = new List<IList<object>> { rowData } };
                var appendRequest = service.Spreadsheets.Values.Append(appendValue, _options.SpreadsheetId, $"{OrdersSheet}!A:{ColToLetter(ColAddonIds)}");
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                await appendRequest.ExecuteAsync(cancellationToken);
            }

            await AppendHistoryAsync(service, rowData, action, cancellationToken);

            await UpdateAddonsSheetAsync(service, cancellationToken);

            _logger.LogInformation("Order {OrderId} {Action} in Google Sheets", order.Id, action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save order {OrderId} to Google Sheets", order.Id);
            throw;
        }
    }

    public async Task DeleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeSheetsAsync(cancellationToken);

            var service = GetSheetsService();
            var orderIdStr = orderId.ToString();
            var (existingRows, existingRowIndex) = await FindOrderRowAsync(service, OrdersSheet, orderIdStr, cancellationToken);

            if (!existingRowIndex.HasValue)
            {
                _logger.LogWarning("Order {OrderId} not found in Google Sheets for deletion", orderId);
                return;
            }

            var deletedRow = existingRows[existingRowIndex.Value - 1];
            var sheetId = await GetSheetIdAsync(service, OrdersSheet, cancellationToken);

            var deleteRequest = new Request
            {
                DeleteDimension = new DeleteDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = sheetId,
                        Dimension = "ROWS",
                        StartIndex = existingRowIndex.Value - 1, // 0-based for API
                        EndIndex = existingRowIndex.Value
                    }
                }
            };

            await service.Spreadsheets.BatchUpdate(
                new BatchUpdateSpreadsheetRequest { Requests = new List<Request> { deleteRequest } },
                _options.SpreadsheetId).ExecuteAsync(cancellationToken);

            var historyRow = new List<object>();
            for (int i = 0; i <= ColAddonIds; i++)
            {
                historyRow.Add(i < deletedRow.Count ? (deletedRow[i]?.ToString() ?? "") : "");
            }
            await AppendHistoryAsync(service, historyRow, "Deleted", cancellationToken);

            await UpdateAddonsSheetAsync(service, cancellationToken);

            _logger.LogInformation("Order {OrderId} deleted from Google Sheets", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete order {OrderId} from Google Sheets", orderId);
            throw;
        }
    }

    private SheetsService GetSheetsService()
    {
        if (_sheetsService != null)
            return _sheetsService;

#pragma warning disable CS0618
        var credential = GoogleCredential.FromJson(_options.CredentialsJson);
#pragma warning restore CS0618
        credential = credential.CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "FanFundingBot"
        });

        return _sheetsService;
    }

    private async Task InitializeSheetsAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        lock (_initLock)
        {
            if (_initialized)
                return;
        }

        var service = GetSheetsService();

        var spreadsheet = await service.Spreadsheets.Get(_options.SpreadsheetId).ExecuteAsync(cancellationToken);
        var existingSheets = spreadsheet.Sheets?.Select(s => s.Properties?.Title).ToHashSet() ?? [];

        var requests = new List<Request>();

        if (!existingSheets.Contains(OrdersSheet))
            requests.Add(new Request { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = OrdersSheet } } });

        if (!existingSheets.Contains(OrdersHistorySheet))
            requests.Add(new Request { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = OrdersHistorySheet } } });

        if (!existingSheets.Contains(AddonsSheet))
            requests.Add(new Request { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = AddonsSheet } } });

        if (requests.Count > 0)
        {
            await service.Spreadsheets.BatchUpdate(
                new BatchUpdateSpreadsheetRequest { Requests = requests },
                _options.SpreadsheetId).ExecuteAsync(cancellationToken);
        }

        await EnsureHeadersAsync(service, OrdersSheet, OrdersHeaders, cancellationToken);
        await EnsureHeadersAsync(service, OrdersHistorySheet, HistoryHeaders, cancellationToken);
        await EnsureHeadersAsync(service, AddonsSheet, AddonsHeaders, cancellationToken);

        _initialized = true;
    }

    private async Task EnsureHeadersAsync(SheetsService service, string sheetName, string[] headers, CancellationToken cancellationToken)
    {
        var range = $"{sheetName}!A1:{ColToLetter(headers.Length - 1)}1";
        var response = await service.Spreadsheets.Values.Get(_options.SpreadsheetId, range).ExecuteAsync(cancellationToken);

        if (response.Values == null || response.Values.Count == 0 || response.Values[0].Count == 0 ||
            response.Values[0][0]?.ToString() != headers[0])
        {
            var valueRange = new ValueRange { Values = new List<IList<object>> { headers.Cast<object>().ToList() } };
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, _options.SpreadsheetId, range);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateRequest.ExecuteAsync(cancellationToken);
        }
    }

    private async Task<(IList<IList<object>> Rows, int? RowIndex)> FindOrderRowAsync(SheetsService service, string sheetName, string orderId, CancellationToken cancellationToken = default)
    {
        var range = $"{sheetName}!A:{ColToLetter(ColAddonIds)}";
        var response = await service.Spreadsheets.Values.Get(_options.SpreadsheetId, range).ExecuteAsync(cancellationToken);

        var rows = response.Values ?? new List<IList<object>>();

        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].Count > 0 && rows[i][0]?.ToString() == orderId)
                return (rows, i + 1); // 1-based row number for Google Sheets API
        }

        return (rows, null);
    }

    private async Task AppendHistoryAsync(SheetsService service, IList<object> rowData, string action, CancellationToken cancellationToken = default)
    {
        var historyRow = new List<object>(rowData);
        while (historyRow.Count <= ColAddonIds)
            historyRow.Add("");

        historyRow.Add(action);
        historyRow.Add(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

        var valueRange = new ValueRange { Values = new List<IList<object>> { historyRow } };
        var appendRequest = service.Spreadsheets.Values.Append(valueRange, _options.SpreadsheetId, $"{OrdersHistorySheet}!A:{ColToLetter(ColChangedAt)}");
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await appendRequest.ExecuteAsync(cancellationToken);
    }

    private async Task UpdateAddonsSheetAsync(SheetsService service, CancellationToken cancellationToken = default)
    {
        var range = $"{OrdersSheet}!A:{ColToLetter(ColAddonIds)}";
        var response = await service.Spreadsheets.Values.Get(_options.SpreadsheetId, range).ExecuteAsync(cancellationToken);
        var rows = response.Values ?? new List<IList<object>>();

        var addonTotals = new Dictionary<(string Name, string AddonId, string Price), int>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count <= ColAddonIds) continue;

            var addonsStr = row[ColAddons]?.ToString();
            var addonIdsStr = row[ColAddonIds]?.ToString();
            if (string.IsNullOrWhiteSpace(addonsStr)) continue;

            var addonLines = addonsStr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var idLines = (addonIdsStr ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int j = 0; j < addonLines.Length; j++)
            {
                var line = addonLines[j].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var crossIdx = line.IndexOf(" × ", StringComparison.Ordinal);
                if (crossIdx < 0) continue;

                var name = line[..crossIdx].Trim();
                var qtyStr = line[(crossIdx + 3)..].Trim();

                var idLine = j < idLines.Length ? idLines[j].Trim() : "";
                var idParts = idLine.Split('|');
                var addonId = idParts.Length > 0 ? idParts[0] : "";
                var priceStr = idParts.Length > 1 ? idParts[1] : "0";

                if (int.TryParse(qtyStr, out var qty))
                {
                    var key = (name, addonId, priceStr);
                    addonTotals.TryGetValue(key, out var current);
                    addonTotals[key] = current + qty;
                }
            }
        }

        var addonRows = addonTotals
            .OrderBy(x => x.Key.Name)
            .Select(x => (IList<object>)new List<object> { x.Key.Name, x.Key.AddonId, $"{x.Key.Price} ₽", x.Value })
            .ToList();

        var addonSheetId = await GetSheetIdAsync(service, AddonsSheet, cancellationToken);

        var clearRequest = new Request
        {
            DeleteRange = new DeleteRangeRequest
            {
                Range = new GridRange
                {
                    SheetId = addonSheetId,
                    StartRowIndex = 1,
                    EndRowIndex = 10000
                },
                ShiftDimension = "ROWS"
            }
        };

        await service.Spreadsheets.BatchUpdate(
            new BatchUpdateSpreadsheetRequest { Requests = new List<Request> { clearRequest } },
            _options.SpreadsheetId).ExecuteAsync(cancellationToken);

        if (addonRows.Count > 0)
        {
            var writeRange = $"{AddonsSheet}!A2:D{addonRows.Count + 1}";
            var writeValue = new ValueRange { Values = addonRows };
            var writeRequest = service.Spreadsheets.Values.Update(writeValue, _options.SpreadsheetId, writeRange);
            writeRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await writeRequest.ExecuteAsync(cancellationToken);
        }
    }

    private async Task<int> GetSheetIdAsync(SheetsService service, string sheetName, CancellationToken cancellationToken = default)
    {
        var spreadsheet = await service.Spreadsheets.Get(_options.SpreadsheetId).ExecuteAsync(cancellationToken);
        return spreadsheet.Sheets?.FirstOrDefault(s => s.Properties?.Title == sheetName)?.Properties?.SheetId ?? -1;
    }

    private IList<object> OrderToRow(Order order)
    {
        var telegramNick = order.User?.Username is { Length: > 0 } username ? "@" + username : "";
        var userName = $"{order.User?.FirstName ?? ""} {order.User?.LastName ?? ""}".Trim();
        if (string.IsNullOrEmpty(userName))
            userName = order.User?.Id.ToString() ?? "";

        var projectName = order.Project?.Name ?? order.Project?.Id.ToString() ?? "";

        var addonsParts = new List<string>();
        var addonIdsParts = new List<string>();
        var totalCost = (decimal)order.Total;

        foreach (var oa in order.OrderAddons)
        {
            var addonName = oa.Addon?.Name ?? oa.AddonId.ToString();
            var addonId = oa.Addon?.Id ?? oa.AddonId;
            var price = oa.Addon?.Price ?? 0m;
            addonsParts.Add($"{addonName} × {oa.Quantity}");
            addonIdsParts.Add($"{addonId}|{price:F2}");
        }

        return new List<object>
        {
            order.Id.ToString(),
            telegramNick,
            userName,
            order.Phone,
            projectName,
            order.DeliveryMethod.ToString(),
            order.Address ?? "",
            order.Status.ToString(),
            order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            $"{totalCost:F2} ₽",
            string.Join("\n", addonsParts),
            string.Join("\n", addonIdsParts)
        };
    }

    private static string ColToLetter(int index)
    {
        var result = "";
        while (index >= 0)
        {
            result = (char)('A' + (index % 26)) + result;
            index = index / 26 - 1;
        }
        return result;
    }
}
