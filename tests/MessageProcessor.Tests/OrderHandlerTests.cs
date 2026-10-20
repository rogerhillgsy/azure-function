using MessageProcessor.Handlers;
using MessageProcessor.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MessageProcessor.Contacts;

namespace MessageProcessor.Tests;

public class OrderHandlerTests
{
    [Fact]
    public async Task Valid_order_is_processed()
    {
        var contactId = Guid.NewGuid();
        var contactService = new StubContactService(
            new ContactInfo(contactId, "Test Contact", "123", "test@example.com"));
        var sut = new OrderHandler(contactService, NullLogger<OrderHandler>.Instance);
        var order = new OrderMessage("ord-1", contactId.ToString(), 42.50m, DateTimeOffset.UtcNow);

        await sut.HandleAsync(order, CancellationToken.None);
        Assert.Equal(contactId, contactService.RequestedContactId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_order_id_is_rejected(string orderId)
    {
        var sut = new OrderHandler(new StubContactService(null), NullLogger<OrderHandler>.Instance);
        var order = new OrderMessage(orderId, Guid.NewGuid().ToString(), 10m, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_amount_is_rejected(decimal amount)
    {
        var sut = new OrderHandler(new StubContactService(null), NullLogger<OrderHandler>.Instance);
        var order = new OrderMessage("ord-1", Guid.NewGuid().ToString(), amount, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_customer_id_is_rejected()
    {
        var sut = new OrderHandler(new StubContactService(null), NullLogger<OrderHandler>.Instance);
        var order = new OrderMessage("ord-1", "not-a-guid", 10m, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_contact_is_rejected()
    {
        var sut = new OrderHandler(new StubContactService(null), NullLogger<OrderHandler>.Instance);
        var order = new OrderMessage("ord-1", Guid.NewGuid().ToString(), 10m, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    private sealed class StubContactService(ContactInfo? contact) : IContactService
    {
        public Guid? RequestedContactId { get; private set; }

        public Task<ContactInfo?> GetByIdAsync(Guid contactId, CancellationToken cancellationToken)
        {
            RequestedContactId = contactId;
            return Task.FromResult(contact);
        }
    }
}
