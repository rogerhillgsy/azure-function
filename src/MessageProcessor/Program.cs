using MessageProcessor.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);
// After creating the builder:
builder.Logging.AddFilter("Azure.Identity", LogLevel.Warning);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddScoped<IOrderHandler, OrderHandler>();

builder.Build().Run();
