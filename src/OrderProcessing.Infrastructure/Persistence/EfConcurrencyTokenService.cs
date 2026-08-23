using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;

namespace OrderProcessing.Infrastructure.Persistence;

internal sealed class EfConcurrencyTokenService : IConcurrencyTokenService
{
    private const string RowVersionProperty = "RowVersion";
    private readonly OrderProcessingDbContext _dbContext;

    public EfConcurrencyTokenService(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string? GetToken(object entity)
    {
        var entry = _dbContext.Entry(entity);
        if (entry.Metadata.FindProperty(RowVersionProperty) is null)
        {
            return null;
        }

        var value = entry.Property(RowVersionProperty).CurrentValue as byte[];
        return value is { Length: > 0 } ? Convert.ToBase64String(value) : null;
    }

    public void SetExpectedToken(object entity, string token)
    {
        var normalized = token.Trim().Trim('"');
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(normalized);
        }
        catch (FormatException)
        {
            throw new ConflictException("Invalid concurrency token.");
        }

        var entry = _dbContext.Entry(entity);
        if (entry.Metadata.FindProperty(RowVersionProperty) is null)
        {
            throw new ConflictException("Concurrency is not supported for this resource.");
        }

        entry.Property(RowVersionProperty).OriginalValue = bytes;
    }
}
