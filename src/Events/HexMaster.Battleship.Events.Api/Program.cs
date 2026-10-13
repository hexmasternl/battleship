using HexMaster.Battleship.Events.Api.Endpoints;
using HexMaster.Battleship.Events.Api.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddEventsCore();
builder.Services.AddSingleton<IConnectionIdentityResolver, QueryStringIdentityResolver>();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapEventStreamEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
