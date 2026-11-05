namespace RentalHive.Application.Contracts;

/// <summary>Time source; "today" is the Swedish calendar date.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Protects personnummer at rest: keyed hash for lookups + authenticated encryption.</summary>
public interface IPersonalNumberProtector
{
    string Hash(string normalizedPersonalNumber);
    string Encrypt(string normalizedPersonalNumber);
    string Decrypt(string cipher);
}

/// <summary>Stores listing photos. Validates content and generates file names.</summary>
public interface IImageStore
{
    Task<StoredImage> SaveAsync(Stream content, long length, CancellationToken ct = default);
    void Delete(string fileName);
    string? ResolvePath(string fileName);
}

public sealed record StoredImage(string FileName, string ContentType);

/// <summary>An outgoing plain-text email.</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Queues email for background delivery, so a slow or unavailable mail server never
/// delays or fails a request. Delivery problems are logged, not thrown.
/// </summary>
public interface IEmailSender
{
    void Enqueue(EmailMessage message);
}
