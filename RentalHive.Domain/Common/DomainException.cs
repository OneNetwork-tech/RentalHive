namespace RentalHive.Domain.Common;

/// <summary>A business rule was violated. Maps to HTTP 400/409/422 at the edges.</summary>
public class DomainException : Exception
{
    public DomainException(string message, DomainErrorKind kind = DomainErrorKind.Invalid) : base(message) => Kind = kind;

    public DomainException() : base("A business rule was violated.") { }

    public DomainException(string message, Exception innerException) : base(message, innerException) { }

    public DomainErrorKind Kind { get; }
}

public enum DomainErrorKind
{
    Invalid,
    NotFound,
    Forbidden,
    Conflict,
}
