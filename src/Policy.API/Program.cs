using eShop.Policy.API.Apis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationServices();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseStatusCodePages();

app.MapPolicyApi();

app.Run();

// Public so the functional tests can build a WebApplicationFactory<Program>.
public partial class Program { }
