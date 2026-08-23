namespace OrderProcessing.Application.Abstractions;

/// <summary>
/// Reads/writes EF shadow RowVersion tokens as opaque base64 strings for optimistic concurrency.
/// </summary>
public interface IConcurrencyTokenService
{
    string? GetToken(object entity);

    void SetExpectedToken(object entity, string token);
}
