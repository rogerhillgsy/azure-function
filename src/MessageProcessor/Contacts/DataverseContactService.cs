using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;

namespace MessageProcessor.Contacts;

public sealed class DataverseContactService(
    IOrganizationServiceAsync2 organizationService) : IContactService
{
    public async Task<ContactInfo?> GetByIdAsync(Guid contactId, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("contact")
        {
            ColumnSet = new ColumnSet("fullname", "mobilephone", "emailaddress1"),
            TopCount = 1,
        };
        query.Criteria.AddCondition("contactid", ConditionOperator.Equal, contactId);

        var results = await organizationService.RetrieveMultipleAsync(query, cancellationToken);
        var contact = results.Entities.SingleOrDefault();

        return contact is null
            ? null
            : new ContactInfo(
                contact.Id,
                contact.GetAttributeValue<string>("fullname"),
                contact.GetAttributeValue<string>("mobilephone"),
                contact.GetAttributeValue<string>("emailaddress1"));
    }
}
