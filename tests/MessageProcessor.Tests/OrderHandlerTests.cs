using MessageProcessor.Handlers;
using MessageProcessor.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MessageProcessor.Tests;

public class OrderHandlerTests
{
    private readonly OrderHandler _sut = new(NullLogger<OrderHandler>.Instance);

    [Fact]
    public async Task Valid_order_is_processed()
    {
        var order = new OrderMessage("ord-1", "cust-1", 42.50m, DateTimeOffset.UtcNow);

        await _sut.HandleAsync(order, CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_order_id_is_rejected(string orderId)
    {
        var order = new OrderMessage(orderId, "cust-1", 10m, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => _sut.HandleAsync(order, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_amount_is_rejected(decimal amount)
    {
        var order = new OrderMessage("ord-1", "cust-1", amount, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => _sut.HandleAsync(order, CancellationToken.None));
    }
}
