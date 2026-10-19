using MessageProcessor.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// After creating the builder:
builder.Logging.AddFilter("Azure.Identity", LogLevel.Warning);
builder.Logging.AddFilter("Azure.Identity.1", LogLevel.Warning);

builder.Services.AddScoped<IOrderHandler, OrderHandler>();

builder.Build().Run();
