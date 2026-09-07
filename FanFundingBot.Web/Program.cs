using FanFundingBot.Application;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Infrastructure.Persistence;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddDataLayer(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapGet("/projects", async (IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetProjectsQuery(), ct);
    if (result.IsFailed)
        return Results.BadRequest(result.Errors.Select(e => e.Message));

    return Results.Ok(result.Value);
})
.WithName("GetProjects")
.WithSummary("Публичная витрина проектов");

app.Run();
