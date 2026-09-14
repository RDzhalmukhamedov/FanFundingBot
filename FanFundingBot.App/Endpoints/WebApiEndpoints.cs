using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Application.Questions.Commands;
using FanFundingBot.Application.Questions.Queries;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Infrastructure.ExternalStorage;
using FluentResults;
using MediatR;

namespace FanFundingBot.App.Endpoints;

public sealed record CreateProjectRequest(string Name, string Description, List<NewAddonItem>? Addons);
public sealed record UpdateProjectRequest(string Name, string Description);
public sealed record UpdateProjectStatusRequest(int StatusId);
public sealed record BroadcastRequest(string Message);
public sealed record UpdateOrderStatusRequest(string Status);
public sealed record DirectMessageRequest(string Text);
public sealed record AnswerQuestionRequest(string Answer);
public sealed record CreateAddonRequest(string Name, string? Description, decimal Price);
public sealed record UpdateAddonRequest(string? Name, string? Description, decimal? Price);

public static class WebApiEndpoints
{
    public static IEndpointRouteBuilder MapWebApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/projects", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProjectsQuery(), ct);
            return ToResult(result);
        });

        api.MapGet("/projects/manageable", async (IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetManageableProjectsQuery(me.GetCurrentUserId()), ct);
            return ToResult(result);
        });

        api.MapGet("/projects/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProjectByIdQuery(id), ct);
            return ToResult(result);
        });

        api.MapPost("/projects", async (CreateProjectRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CreateProjectCommand(
                me.GetCurrentUserId(), request.Name, request.Description, request.Addons), ct);
            return ToResult(result, id => Results.Created($"/api/projects/{id}", new { id }));
        });

        api.MapPut("/projects/{id:guid}", async (Guid id, UpdateProjectRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var uid = me.GetCurrentUserId();
            var renamed = await mediator.Send(new RenameProjectCommand(uid, id, request.Name), ct);
            if (renamed.IsFailed)
                return Failed(renamed);

            var described = await mediator.Send(new UpdateProjectDescriptionCommand(uid, id, request.Description), ct);
            return ToResult(described);
        });

        api.MapDelete("/projects/{id:guid}", async (Guid id, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteProjectCommand(me.GetCurrentUserId(), id), ct);
            return ToResult(result, () => Results.NoContent());
        });

        api.MapPut("/projects/{id:guid}/status", async (Guid id, UpdateProjectStatusRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new UpdateProjectStatusCommand(me.GetCurrentUserId(), id, request.StatusId), ct);
            return ToResult(result);
        });

        api.MapPost("/projects/{id:guid}/broadcast", async (Guid id, BroadcastRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new BroadcastCommand(me.GetCurrentUserId(), id, request.Message), ct);
            return ToResult(result);
        });

        api.MapGet("/projects/{id:guid}/orders", async (Guid id, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetOrdersByProjectQuery(id, me.GetCurrentUserId()), ct);
            return ToResult(result);
        });

        api.MapGet("/projects/{id:guid}/sheet-link", async (
            Guid id, IAuthorizationService authorization, ICurrentUserProvider me, GoogleSheetsOptions sheets) =>
        {
            if (!await authorization.CanManageProjectAsync(me.GetCurrentUserId(), id))
                return Results.Forbid();

            return Results.Ok(new
            {
                url = $"https://docs.google.com/spreadsheets/d/{sheets.SpreadsheetId}"
            });
        });

        api.MapPut("/orders/{id:guid}/status", async (Guid id, UpdateOrderStatusRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            if (!Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var status))
                return Results.BadRequest(new[] { "Некорректный статус заказа." });

            var result = await mediator.Send(new UpdateOrderStatusCommand(id, me.GetCurrentUserId(), status), ct);
            return ToResult(result);
        });

        api.MapPost("/orders/{id:guid}/message", async (Guid id, DirectMessageRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SendDirectMessageCommand(me.GetCurrentUserId(), id, request.Text), ct);
            return ToResult(result);
        });

        api.MapGet("/questions/unanswered", async (IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetUnansweredQuestionsQuery(me.GetCurrentUserId()), ct);
            return ToResult(result);
        });

        api.MapPost("/questions/{id:guid}/answer", async (Guid id, AnswerQuestionRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new AnswerQuestionCommand(me.GetCurrentUserId(), id, request.Answer), ct);
            return ToResult(result);
        });

        api.MapGet("/my-orders", async (IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyOrdersQuery(me.GetCurrentUserId()), ct);
            return ToResult(result);
        });

        api.MapGet("/project-statuses", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProjectStatusesQuery(), ct);
            return ToResult(result);
        });

        api.MapPost("/projects/{id:guid}/addons", async (Guid id, CreateAddonRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CreateAddonCommand(
                me.GetCurrentUserId(), id, request.Name, request.Description ?? string.Empty, request.Price), ct);
            return ToResult(result, addonId => Results.Created($"/api/addons/{addonId}", new { id = addonId }));
        });

        api.MapPut("/addons/{id:guid}", async (Guid id, UpdateAddonRequest request, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var uid = me.GetCurrentUserId();
            if (request.Name is not null)
            {
                var renamed = await mediator.Send(new UpdateAddonNameCommand(uid, id, request.Name), ct);
                if (renamed.IsFailed)
                    return Failed(renamed);
            }

            if (request.Description is not null)
            {
                var described = await mediator.Send(new UpdateAddonDescriptionCommand(uid, id, request.Description), ct);
                if (described.IsFailed)
                    return Failed(described);
            }

            if (request.Price.HasValue)
            {
                var priced = await mediator.Send(new UpdateAddonPriceCommand(uid, id, request.Price.Value), ct);
                if (priced.IsFailed)
                    return Failed(priced);
            }

            return Results.Ok();
        });

        api.MapDelete("/addons/{id:guid}", async (Guid id, IMediator mediator, ICurrentUserProvider me, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteAddonCommand(me.GetCurrentUserId(), id), ct);
            return ToResult(result, () => Results.NoContent());
        });

        return app;
    }

    private static IResult ToResult<T>(Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value);

        return Failed(result);
    }

    private static IResult ToResult(Result result, Func<IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess?.Invoke() ?? Results.Ok();

        return Failed(result);
    }

    private static IResult Failed(ResultBase result)
    {
        var messages = result.Errors.Select(e => e.Message);
        if (result.IsNotFound())
            return Results.NotFound(messages);
        if (result.IsForbidden())
            return Results.Forbid();
        if (result.IsConflict())
            return Results.Conflict(messages);

        return Results.BadRequest(messages);
    }
}
