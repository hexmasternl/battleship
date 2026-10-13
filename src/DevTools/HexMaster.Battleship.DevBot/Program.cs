var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the tests.
public partial class Program;
