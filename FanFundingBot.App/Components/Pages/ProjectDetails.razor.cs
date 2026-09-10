using System.Net.Http.Json;
using System.Text.Json;
using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Questions.DTOs;
using FanFundingBot.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace FanFundingBot.App.Components.Pages;

public partial class ProjectDetails
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpFactory { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private ProjectDto? Project { get; set; }
    private List<OrderDto> Orders { get; set; } = [];
    private List<QuestionDto> Questions { get; set; } = [];
    private List<ProjectStatusDto> Statuses { get; set; } = [];
    private Dictionary<Guid, string> Answers { get; set; } = new();
    private string? SheetUrl { get; set; }
    private string? Error { get; set; }
    private string? ModalError { get; set; }

    private int NewStatusId { get; set; }
    private bool ProjectStatusConfirm { get; set; }
    private string NewStatusName =>
        Statuses.FirstOrDefault(s => s.Id == NewStatusId)?.DisplayName ?? string.Empty;

    private OrderDto? OrderStatusTarget { get; set; }
    private OrderStatus OrderStatusValue { get; set; }
    private OrderDto? MessageTarget { get; set; }
    private bool BroadcastOpen { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            Nav.NavigateTo("/", forceLoad: true);
            return;
        }

        var client = WebApi.Client(HttpFactory, Nav);
        try
        {
            Statuses = await client.GetFromJsonAsync<List<ProjectStatusDto>>("api/project-statuses") ?? [];
            await ReloadAsync();
        }
        catch
        {
            Error = "Не удалось загрузить проект. Попробуйте позже.";
        }
    }

    private async Task ReloadAsync()
    {
        var client = WebApi.Client(HttpFactory, Nav);
        Project = await client.GetFromJsonAsync<ProjectDto>($"api/projects/{Id}");
        Orders = await client.GetFromJsonAsync<List<OrderDto>>($"api/projects/{Id}/orders") ?? [];
        Questions = await client.GetFromJsonAsync<List<QuestionDto>>("api/questions/unanswered") ?? [];
        Answers = Questions.ToDictionary(q => q.Id, _ => string.Empty);

        if (Project is not null && !Statuses.Any(s => s.Id == NewStatusId))
            NewStatusId = Project.StatusId;

        try
        {
            var link = await client.GetFromJsonAsync<JsonElement>($"api/projects/{Id}/sheet-link");
            SheetUrl = link.GetProperty("url").GetString();
        }
        catch
        {
            SheetUrl = string.Empty;
        }
    }

    private void AskProjectStatus() { ModalError = null; ProjectStatusConfirm = true; }
    private void EditProject() => Nav.NavigateTo($"/projects/{Id}/edit");

    private async Task ChangeProjectStatusAsync()
    {
        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PutAsJsonAsync($"api/projects/{Id}/status", new { statusId = NewStatusId });
        if (response.IsSuccessStatusCode)
        {
            ProjectStatusConfirm = false;
            await ReloadAsync();
        }
        else
        {
            ModalError = await WebApi.ReadErrorAsync(response);
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

        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PutAsJsonAsync(
            $"api/orders/{OrderStatusTarget.Id}/status", new { status = OrderStatusValue.ToString() });
        if (response.IsSuccessStatusCode)
        {
            OrderStatusTarget = null;
            await ReloadAsync();
        }
        else
        {
            ModalError = await WebApi.ReadErrorAsync(response);
        }
    }

    private void AskMessage(OrderDto order) { MessageTarget = order; ModalError = null; }

    private async Task SendMessageAsync(string text)
    {
        if (MessageTarget is null)
            return;

        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PostAsJsonAsync($"api/orders/{MessageTarget.Id}/message", new { text });
        if (response.IsSuccessStatusCode)
        {
            MessageTarget = null;
        }
        else
        {
            ModalError = await WebApi.ReadErrorAsync(response);
        }
    }

    private void AskBroadcast() { BroadcastOpen = true; ModalError = null; }

    private async Task BroadcastAsync(string text)
    {
        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PostAsJsonAsync($"api/projects/{Id}/broadcast", new { message = text });
        if (response.IsSuccessStatusCode)
        {
            BroadcastOpen = false;
        }
        else
        {
            ModalError = await WebApi.ReadErrorAsync(response);
        }
    }

    private async Task AnswerAsync(Guid questionId)
    {
        var client = WebApi.Client(HttpFactory, Nav);
        var response = await client.PostAsJsonAsync(
            $"api/questions/{questionId}/answer", new { answer = Answers.GetValueOrDefault(questionId) ?? string.Empty });
        if (response.IsSuccessStatusCode)
            await ReloadAsync();
        else
            Error = await WebApi.ReadErrorAsync(response);
    }

    // Дублирует маппинг StatusBadge (там — компонент, здесь — текст для <option>).
    private static string OrderStatusText(OrderStatus status) => status switch
    {
        OrderStatus.New => "🆕 Новый",
        OrderStatus.Confirmed => "✅ Подтверждён",
        OrderStatus.Completed => "✔️ Выполнен",
        OrderStatus.Cancelled => "❌ Отменён",
        _ => status.ToString()
    };
}
