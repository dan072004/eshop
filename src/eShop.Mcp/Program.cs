using eShop.Mcp;
using eShop.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBuyerAuthentication();
builder.AddMcpServer();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp(McpServerExtensions.McpPath).RequireAuthorization();

app.Run();

// Public so the functional tests can build a WebApplicationFactory<Program>.
public partial class Program { }
