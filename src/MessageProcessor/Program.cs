using Azure.Core;
using Azure.Identity;
using MessageProcessor.Contacts;
using MessageProcessor.Handlers;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
builder.Services.AddSingleton<ServiceClient>(serviceProvider =>
{
    var dataverseOptions = serviceProvider.GetRequiredService<IOptions<DataverseOptions>>().Value;
    var credential = serviceProvider.GetRequiredService<TokenCredential>();
    var serviceClient = new ServiceClient(new Uri(dataverseOptions.Url), async instanceUri =>
    {
        var dataverseUri = new Uri(instanceUri);
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([$"{dataverseUri.GetLeftPart(UriPartial.Authority)}/.default"]),
            CancellationToken.None);
        return token.Token;
    });

    if (!serviceClient.IsReady)
    {
        var error = serviceClient.LastError;
        serviceClient.Dispose();
        throw new InvalidOperationException($"Could not connect to Dataverse: {error}");
    }

    return serviceClient;
});
builder.Services.AddSingleton<IOrganizationServiceAsync2>(serviceProvider =>
    serviceProvider.GetRequiredService<ServiceClient>());
builder.Services.AddScoped<IContactService, DataverseContactService>();
builder.Services.AddScoped<IOrderHandler, OrderHandler>();

builder.Build().Run();
