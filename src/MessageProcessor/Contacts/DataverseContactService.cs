using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Extensions.Options;

namespace MessageProcessor.Contacts;

public sealed class DataverseContactService(
    HttpClient httpClient,
    TokenCredential credential,
    IOptions<DataverseOptions> options) : IContactService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Uri _environmentUrl = new(options.Value.Url.TrimEnd('/') + "/");

    public async Task<ContactInfo?> GetByIdAsync(Guid contactId, CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([$"{_environmentUrl.GetLeftPart(UriPartial.Authority)}/.default"]),
            cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(_environmentUrl,
                $"api/data/v9.2/contacts({contactId:D})?$select=fullname,mobilephone,emailaddress1"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("OData-Version", "4.0");
        request.Headers.Add("OData-MaxVersion", "4.0");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dataverseContact = await response.Content.ReadFromJsonAsync<DataverseContact>(
            JsonOptions,
            cancellationToken);
        if (dataverseContact is null)
        {
            throw new InvalidDataException("Dataverse returned an empty contact response.");
        }

        return new ContactInfo(
            contactId,
            dataverseContact.FullName,
            dataverseContact.MobilePhone,
            dataverseContact.EmailAddress);
    }

    private sealed class DataverseContact
    {
        [JsonPropertyName("fullname")]
        public string? FullName { get; init; }

        [JsonPropertyName("mobilephone")]
        public string? MobilePhone { get; init; }

        [JsonPropertyName("emailaddress1")]
        public string? EmailAddress { get; init; }
    }
}
