namespace MessageProcessor.Contacts;

public sealed record ContactInfo(
    Guid Id,
    string? FullName,
    string? MobilePhone,
    string? EmailAddress);
