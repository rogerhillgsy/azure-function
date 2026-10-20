using Microsoft.Xrm.Sdk;
using FakeXrmEasy.Abstractions;
using MessageProcessor.Contacts;
using MessageProcessor.Handlers;
using MessageProcessor.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MessageProcessor.Tests;

public class OrderHandlerTests
{
    [Fact]
    public async Task Valid_order_is_processed()
    {
        var contactId = Guid.NewGuid();
        var context = FakeXrmContextFactory.Create();
        context.Initialize(new Entity("contact", contactId)
        {
            ["fullname"] = "Test Contact",
            ["mobilephone"] = "123",
            ["emailaddress1"] = "test@example.com",
        });
        var sut = CreateHandler(context);
        var order = new OrderMessage("ord-1", contactId.ToString(), 42.50m, DateTimeOffset.UtcNow);

        await sut.HandleAsync(order, CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_order_id_is_rejected(string orderId)
    {
        var sut = CreateHandler(FakeXrmContextFactory.Create());
        var order = new OrderMessage(orderId, Guid.NewGuid().ToString(), 10m, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_amount_is_rejected(decimal amount)
    {
        var sut = CreateHandler(FakeXrmContextFactory.Create());
        var order = new OrderMessage("ord-1", Guid.NewGuid().ToString(), amount, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_customer_id_is_rejected()
    {
        var sut = CreateHandler(FakeXrmContextFactory.Create());
        var order = new OrderMessage("ord-1", "not-a-guid", 10m, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_contact_is_rejected()
    {
        var sut = CreateHandler(FakeXrmContextFactory.Create());
        var order = new OrderMessage("ord-1", Guid.NewGuid().ToString(), 10m, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOrderException>(() => sut.HandleAsync(order, CancellationToken.None));
    }

    private static OrderHandler CreateHandler(IXrmFakedContext context) =>
        new(new DataverseContactService(context.GetAsyncOrganizationService2()),
            NullLogger<OrderHandler>.Instance);
}
