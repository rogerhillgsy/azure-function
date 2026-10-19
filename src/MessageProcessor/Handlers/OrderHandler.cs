using MessageProcessor.Contacts;
using MessageProcessor.Models;
using Microsoft.Extensions.Logging;

namespace MessageProcessor.Handlers;

public interface IOrderHandler
{
    Task HandleAsync(OrderMessage order, CancellationToken cancellationToken);
}

/// <summary>
/// Business logic lives here, kept free of Functions/Service Bus types so it is easy to unit test.
/// Throw <see cref="InvalidOrderException"/> for messages that can never succeed (they get dead-lettered);
/// throw anything else for transient failures (the message is abandoned and retried).
/// </summary>
public sealed class OrderHandler(
    IContactService contactService,
    ILogger<OrderHandler> logger) : IOrderHandler
{
    public async Task HandleAsync(OrderMessage order, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.OrderId))
        {
            throw new InvalidOrderException("OrderId is required.");
        }

        if (order.Amount <= 0)
        {
            throw new InvalidOrderException($"Order {order.OrderId} has a non-positive amount ({order.Amount}).");
        }

        if (!Guid.TryParse(order.CustomerId, out var contactId))
        {
            throw new InvalidOrderException($"CustomerId for order {order.OrderId} must be a Dataverse contact GUID.");
        }

        var contact = await contactService.GetByIdAsync(contactId, cancellationToken);
        if (contact is null)
        {
            throw new InvalidOrderException($"No Dataverse contact was found for order {order.OrderId}.");
        }

        logger.LogInformation(
            "Processing order {OrderId} for contact {ContactId}, amount {Amount}",
            order.OrderId, contact.Id, order.Amount);

        // TODO: real work goes here (write to a database, call an API, publish an event, ...)
    }
}

public sealed class InvalidOrderException(string message) : Exception(message);
