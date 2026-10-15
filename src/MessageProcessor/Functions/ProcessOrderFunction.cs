using System.Text.Json;
using Azure.Messaging.ServiceBus;
using MessageProcessor.Handlers;
using MessageProcessor.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MessageProcessor.Functions;

public sealed class ProcessOrderFunction(IOrderHandler handler, ILogger<ProcessOrderFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Triggered for every message on the queue named by the "ServiceBusQueueName" app setting.
    /// "ServiceBusConnection" is an identity-based connection (see ServiceBusConnection__* app settings),
    /// so no connection strings or SAS keys are involved.
    /// </summary>
    [Function(nameof(ProcessOrder))]
    public async Task ProcessOrder(
        [ServiceBusTrigger("%ServiceBusQueueName%", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["CorrelationId"] = message.CorrelationId ?? string.Empty,
            ["DeliveryCount"] = message.DeliveryCount,
        });

        OrderMessage? order;
        try
        {
            order = message.Body.ToObjectFromJson<OrderMessage>(JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Message body is not a valid OrderMessage; dead-lettering");
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "InvalidJson",
                deadLetterErrorDescription: ex.Message, cancellationToken: cancellationToken);
            return;
        }

        if (order is null)
        {
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "EmptyBody",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            await handler.HandleAsync(order, cancellationToken);
            await messageActions.CompleteMessageAsync(message, cancellationToken);
        }
        catch (InvalidOrderException ex)
        {
            logger.LogWarning(ex, "Order failed validation; dead-lettering");
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "ValidationFailed",
                deadLetterErrorDescription: ex.Message, cancellationToken: cancellationToken);
        }
        // Any other exception bubbles up: the message is abandoned and redelivered until the
        // queue's maxDeliveryCount is reached, after which Service Bus dead-letters it.
    }
}
