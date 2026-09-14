using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Application.Questions.Commands;
using FanFundingBot.Application.Questions.DTOs;
using FanFundingBot.Application.Questions.Queries;
using FanFundingBot.App.Components.Shared;
using FanFundingBot.App.Services;using FanFundingBot.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class ProjectDetails : AuthComponentBase
{
    private const int PageSize = 20;

    [Parameter] public Guid Id { get; set; }
    [Inject] private ProjectSheetLinkService SheetLinks { get; set; } = null!;
    [Inject] private IToastService Toasts { get; set; } = null!;
    [Inject] private IDialogService Dialogs { get; set; } = null!;

    private ProjectDto? Project { get; set; }
    private PaginationState _ordersPagination = new() { ItemsPerPage = PageSize };
    private PaginationState _questionsPagination = new() { ItemsPerPage = PageSize };
    private OrdersTable? _ordersTable;
    private QuestionsTable? _questionsTable;
    private int OrdersTotalCount { get; set; }
    private int QuestionsTotalCount { get; set; }
    private List<ProjectStatusDto> Statuses { get; set; } = [];
    private string? SheetUrl { get; set; }
    private string? Error { get; set; }
    private string? ModalError { get; set; }
    private long _userId;

    private string ActiveTab { get; set; } = "overview";

    private int NewStatusId { get; set; }
    private string NewStatusName =>
        Statuses.FirstOrDefault(s => s.Id == NewStatusId)?.DisplayName ?? string.Empty;

    private ProjectStatusDto? SelectedProjectStatus
    {
        get => Statuses.FirstOrDefault(s => s.Id == NewStatusId);
        set
        {
            if (value is not null)
                NewStatusId = value.Id;
        }
    }

    private static List<OrderStatus> OrderStatuses => Enum.GetValues<OrderStatus>().ToList();

    private OrderDto? OrderStatusTarget { get; set; }
    private OrderStatus OrderStatusValue { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (!IsAuthenticated(state.User))
        {
            RedirectToLogin();
            return;
        }

        _userId = await UseUserAsync();
        try
        {
            var statuses = await Mediator.Send(new GetProjectStatusesQuery(), RequestCt);
            if (statuses.IsFailed)
            {
                Error = BlazorResult.Message(statuses);
                return;
            }

            Statuses = statuses.Value;
            await ReloadAsync();
        }
        catch
        {
            Error = "Не удалось загрузить проект. Попробуйте позже.";
        }
    }

    private async Task ReloadAsync()
    {
        var project = await Mediator.Send(new GetProjectByIdQuery(Id), RequestCt);
        if (project.IsFailed)
        {
            Error = BlazorResult.Message(project);
            return;
        }

        Project = project.Value;

        if (Project is not null && !Statuses.Any(s => s.Id == NewStatusId))
            NewStatusId = Project.StatusId;

        var link = await SheetLinks.GetUrlAsync(Id);
        SheetUrl = link.IsSuccess ? link.Value : string.Empty;

        if (_ordersTable is not null)
            await _ordersTable.RefreshAsync();
        if (_questionsTable is not null)
            await _questionsTable.RefreshAsync();
    }

    private async ValueTask<GridItemsProviderResult<OrderDto>> OrdersProvider(GridItemsProviderRequest<OrderDto> request)
    {
        var paged = await Mediator.Send(
            new GetOrdersByProjectQuery(Id, _userId, request.StartIndex / PageSize + 1, PageSize), RequestCt);
        if (paged.IsFailed)
        {
            Error = BlazorResult.Message(paged);
            return GridItemsProviderResult.From(Array.Empty<OrderDto>(), 0);
        }

        OrdersTotalCount = paged.Value.TotalCount;
        return GridItemsProviderResult.From(paged.Value.Items, paged.Value.TotalCount);
    }

    private async ValueTask<GridItemsProviderResult<QuestionDto>> QuestionsProvider(GridItemsProviderRequest<QuestionDto> request)
    {
        var paged = await Mediator.Send(
            new GetUnansweredQuestionsQuery(_userId, request.StartIndex / PageSize + 1, PageSize), RequestCt);
        if (paged.IsFailed)
        {
            Error = BlazorResult.Message(paged);
            return GridItemsProviderResult.From(Array.Empty<QuestionDto>(), 0);
        }

        QuestionsTotalCount = paged.Value.TotalCount;
        return GridItemsProviderResult.From(paged.Value.Items, paged.Value.TotalCount);
    }

    private async Task AskProjectStatusAsync()
    {
        var dialog = await Dialogs.ShowDialogAsync<ConfirmDialog, ConfirmDialogData>(
            new ConfirmDialogData(
                "Смена статуса проекта",
                $"Установить статус «{NewStatusName}»? Подписанным пользователям придёт оповещение."),
            new DialogParameters { Title = "Смена статуса проекта" });
        if ((await dialog.Result).Cancelled)
            return;

        await ChangeProjectStatusAsync();
    }

    private void EditProject() => Nav.NavigateTo($"/projects/{Id}/edit");

    private async Task<string?> ReadDialogTextAsync(string title)
    {
        var dialog = await Dialogs.ShowDialogAsync<MessageDialog, MessageDialogData>(
            new MessageDialogData(title, "Введите сообщение..."),
            new DialogParameters { Title = title });
        var result = await dialog.Result;
        return result.Cancelled ? null : result.Data as string ?? string.Empty;
    }

    private async Task ChangeProjectStatusAsync()
    {
        var result = await Mediator.Send(
            new UpdateProjectStatusCommand(_userId, Id, NewStatusId), RequestCt);
        if (result.IsSuccess)
        {
            Toasts.ShowSuccess("Статус проекта изменён.");
            await ReloadAsync();
        }
        else
        {
            Toasts.ShowError(BlazorResult.Message(result) ?? "Не удалось сменить статус.");
        }
    }

    private void AskOrderStatus(OrderDto order)
    {
        OrderStatusTarget = order;
        OrderStatusValue = order.Status;
        ModalError = null;
    }

    private async Task ChangeOrderStatusAsync()
    {
        if (OrderStatusTarget is null)
            return;

        var result = await Mediator.Send(
            new UpdateOrderStatusCommand(OrderStatusTarget.Id, _userId, OrderStatusValue), RequestCt);
        if (result.IsSuccess)
        {
            OrderStatusTarget = null;
            Toasts.ShowSuccess("Статус заказа изменён.");
            if (_ordersTable is not null)
                await _ordersTable.RefreshAsync();
        }
        else
        {
            ModalError = BlazorResult.Message(result);
        }
    }

    private async Task AskMessageAsync(OrderDto order)
    {
        var text = await ReadDialogTextAsync("Сообщение пользователю");
        if (text is null)
            return;

        await SendMessageAsync(order.Id, text);
    }

    private async Task SendMessageAsync(Guid orderId, string text)
    {
        var result = await Mediator.Send(
            new SendDirectMessageCommand(_userId, orderId, text), RequestCt);
        if (result.IsSuccess)
        {
            Toasts.ShowSuccess("Сообщение отправлено.");
        }
        else
        {
            Toasts.ShowError(BlazorResult.Message(result) ?? "Не удалось отправить сообщение.");
        }
    }

    private async Task AskBroadcastAsync()
    {
        var text = await ReadDialogTextAsync("Рассылка подписанным пользователям");
        if (text is null)
            return;

        await BroadcastAsync(text);
    }

    private async Task BroadcastAsync(string text)
    {
        var result = await Mediator.Send(new BroadcastCommand(_userId, Id, text), RequestCt);
        if (result.IsSuccess)
        {
            Toasts.ShowSuccess("Рассылка отправлена.");
        }
        else
        {
            Toasts.ShowError(BlazorResult.Message(result) ?? "Не удалось отправить рассылку.");
        }
    }

    private async Task AskAnswerAsync(QuestionDto question)
    {
        var text = await ReadDialogTextAsync("Ответ на вопрос");
        if (text is null)
            return;

        await SendAnswerAsync(question.Id, text);
    }

    private async Task SendAnswerAsync(Guid questionId, string text)
    {
        var result = await Mediator.Send(
            new AnswerQuestionCommand(_userId, questionId, text), RequestCt);
        if (result.IsSuccess)
        {
            Toasts.ShowSuccess("Ответ отправлен.");
            if (_questionsTable is not null)
                await _questionsTable.RefreshAsync();
        }
        else
        {
            Toasts.ShowError(BlazorResult.Message(result) ?? "Не удалось отправить ответ.");
        }
    }

}
