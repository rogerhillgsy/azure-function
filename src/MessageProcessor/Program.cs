using Azure.Core;
using Azure.Identity;
using MessageProcessor.Contacts;
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

builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
    var applicationInsightsDefaultRule = options.Rules.FirstOrDefault(rule =>
        rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

    if (applicationInsightsDefaultRule is not null)
    {
        options.Rules.Remove(applicationInsightsDefaultRule);
    }
});

builder.Services.AddOptions<DataverseOptions>()
    .BindConfiguration(DataverseOptions.SectionName)
    .Validate(options =>
        Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps,
        "Dataverse:Url must be an absolute HTTPS URL.")
    .ValidateOnStart();

builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential(
    new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = builder.Configuration["ServiceBusConnection:clientId"],
    }));
builder.Services.AddHttpClient<IContactService, DataverseContactService>();
builder.Services.AddScoped<IOrderHandler, OrderHandler>();

builder.Build().Run();
