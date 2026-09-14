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
using FanFundingBot.App.Services;
using FanFundingBot.Domain.Enums;
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
    private List<OrderDto> Orders { get; set; } = [];
    private int OrdersPage { get; set; } = 1;
    private int OrdersTotalCount { get; set; }
    private int OrdersTotalPages => Math.Max(1, (int)Math.Ceiling(OrdersTotalCount / (double)PageSize));
    private List<QuestionDto> Questions { get; set; } = [];
    private int QuestionsPage { get; set; } = 1;
    private int QuestionsTotalCount { get; set; }
    private int QuestionsTotalPages => Math.Max(1, (int)Math.Ceiling(QuestionsTotalCount / (double)PageSize));
    private List<ProjectStatusDto> Statuses { get; set; } = [];
    private string? SheetUrl { get; set; }
    private string? Error { get; set; }
    private string? ModalError { get; set; }
    private long _userId;

    private string ActiveTab { get; set; } = "overview";

    private int NewStatusId { get; set; }
    private string NewStatusName =>
        Statuses.FirstOrDefault(s => s.Id == NewStatusId)?.DisplayName ?? string.Empty;

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

        OrdersPage = 1;
        QuestionsPage = 1;
        await LoadOrdersAsync();
        await LoadQuestionsAsync();

        var link = await SheetLinks.GetUrlAsync(Id);
        SheetUrl = link.IsSuccess ? link.Value : string.Empty;
    }

    private async Task LoadOrdersAsync()
    {
        var paged = await Mediator.Send(
            new GetOrdersByProjectQuery(Id, _userId, OrdersPage, PageSize), RequestCt);
        if (paged.IsFailed)
        {
            Error = BlazorResult.Message(paged);
            Orders = [];
            OrdersTotalCount = 0;
            return;
        }

        Orders = paged.Value.Items;
        OrdersTotalCount = paged.Value.TotalCount;
        if (Orders.Count == 0 && OrdersPage > 1)
        {
            OrdersPage = Math.Max(1, OrdersTotalPages);
            await LoadOrdersAsync();
        }
    }

    private async Task LoadQuestionsAsync()
    {
        var paged = await Mediator.Send(
            new GetUnansweredQuestionsQuery(_userId, QuestionsPage, PageSize), RequestCt);
        if (paged.IsFailed)
        {
            Error = BlazorResult.Message(paged);
            Questions = [];
            QuestionsTotalCount = 0;
            return;
        }

        Questions = paged.Value.Items;
        QuestionsTotalCount = paged.Value.TotalCount;
        if (Questions.Count == 0 && QuestionsPage > 1)
        {
            QuestionsPage = Math.Max(1, QuestionsTotalPages);
            await LoadQuestionsAsync();
        }
    }

    private async Task OrdersPrevAsync()
    {
        if (OrdersPage > 1)
        {
            OrdersPage--;
            await LoadOrdersAsync();
        }
    }

    private async Task OrdersNextAsync()
    {
        if (OrdersPage < OrdersTotalPages)
        {
            OrdersPage++;
            await LoadOrdersAsync();
        }
    }

    private async Task QuestionsPrevAsync()
    {
        if (QuestionsPage > 1)
        {
            QuestionsPage--;
            await LoadQuestionsAsync();
        }
    }

    private async Task QuestionsNextAsync()
    {
        if (QuestionsPage < QuestionsTotalPages)
        {
            QuestionsPage++;
            await LoadQuestionsAsync();
        }
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
            await LoadOrdersAsync();
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
            await LoadQuestionsAsync();
        }
        else
        {
            Toasts.ShowError(BlazorResult.Message(result) ?? "Не удалось отправить ответ.");
        }
    }

    private static string OrderStatusText(OrderStatus status) =>
        Shared.OrderStatusTexts.Text(status);
}
