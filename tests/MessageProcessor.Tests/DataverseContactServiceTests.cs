using System.Net;
using System.Text;
using Azure.Core;
using MessageProcessor.Contacts;
using Microsoft.Extensions.Options;

namespace MessageProcessor.Tests;

public class DataverseContactServiceTests
{
    [Fact]
    public async Task Reads_requested_contact_fields_from_dataverse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"fullname":"Test Contact","mobilephone":"123","emailaddress1":"test@example.com"}""",
                Encoding.UTF8,
                "application/json"),
        });
        var credential = new StubTokenCredential();
        using var httpClient = new HttpClient(handler);
        var sut = new DataverseContactService(
            httpClient,
            credential,
            Options.Create(new DataverseOptions { Url = "https://org.crm.dynamics.com" }));
        var contactId = Guid.NewGuid();

        var contact = await sut.GetByIdAsync(contactId, CancellationToken.None);

        Assert.Equal(contactId, contact?.Id);
        Assert.Equal("Test Contact", contact?.FullName);
        Assert.Equal("123", contact?.MobilePhone);
        Assert.Equal("test@example.com", contact?.EmailAddress);
        Assert.Equal(
            $"https://org.crm.dynamics.com/api/data/v9.2/contacts({contactId:D})?$select=fullname,mobilephone,emailaddress1",
            handler.RequestUri?.ToString());
        Assert.Equal("https://org.crm.dynamics.com/.default", credential.RequestedScopes.Single());
    }

    [Fact]
    public async Task Returns_null_when_contact_does_not_exist()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        var sut = new DataverseContactService(
            httpClient,
            new StubTokenCredential(),
            Options.Create(new DataverseOptions { Url = "https://org.crm.dynamics.com" }));

        var contact = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(contact);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public string[] RequestedScopes { get; private set; } = [];

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            RequestedScopes = requestContext.Scopes;
            return new AccessToken("test-token", DateTimeOffset.UtcNow.AddMinutes(5));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            RequestedScopes = requestContext.Scopes;
            return ValueTask.FromResult(new AccessToken("test-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
}
