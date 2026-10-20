using MessageProcessor.Contacts;
using Microsoft.Xrm.Sdk;

namespace MessageProcessor.Tests;

public class DataverseContactServiceTests
{
    [Fact]
    public async Task Retrieves_contact_fields_from_fake_xrm_context()
    {
        var contactId = Guid.NewGuid();
        var context = FakeXrmContextFactory.Create();
        context.Initialize(new Entity("contact", contactId)
        {
            ["fullname"] = "Test Contact",
            ["mobilephone"] = "123",
            ["emailaddress1"] = "test@example.com",
        });
        var sut = new DataverseContactService(context.GetAsyncOrganizationService2());

        var contact = await sut.GetByIdAsync(contactId, CancellationToken.None);

        Assert.Equal(contactId, contact?.Id);
        Assert.Equal("Test Contact", contact?.FullName);
        Assert.Equal("123", contact?.MobilePhone);
        Assert.Equal("test@example.com", contact?.EmailAddress);
    }

    [Fact]
    public async Task Returns_null_when_contact_does_not_exist()
    {
        var context = FakeXrmContextFactory.Create();
        var sut = new DataverseContactService(context.GetAsyncOrganizationService2());

        var contact = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(contact);
    }
}
