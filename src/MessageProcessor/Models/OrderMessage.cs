namespace MessageProcessor.Models;

/// <summary>
/// Example payload carried on the queue. Replace with your own contract.
/// </summary>
public sealed record OrderMessage(
    string OrderId,
    string CustomerId,
    decimal Amount,
    DateTimeOffset CreatedAt);
