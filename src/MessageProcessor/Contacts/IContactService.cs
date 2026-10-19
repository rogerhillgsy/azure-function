namespace MessageProcessor.Contacts;

public interface IContactService
{
    Task<ContactInfo?> GetByIdAsync(Guid contactId, CancellationToken cancellationToken);
}
