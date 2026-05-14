using System.Text.Json;
using AFH.Booking.Function.Middleware;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.EntityFramework.Entities;
using AFH.Common.Errors.EntityFramework.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Tests;

public sealed class BookingErrorPersistenceTests
{
    [Fact]
    public void BookingDbContext_ModelIncludesSharedErrorRecordEntity()
    {
        using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType(typeof(ErrorRecordEntity));

        Assert.NotNull(entityType);
    }

    [Fact]
    public void BookingDbContext_ModelIncludesAdviserProfileProjectionPrimaryKey()
    {
        using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType(typeof(AdviserProfileProjectionModel));

        Assert.NotNull(entityType);
        Assert.NotNull(entityType!.FindPrimaryKey());
        Assert.Equal(nameof(AdviserProfileProjectionModel.AdviserId), entityType.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public async Task EntityFrameworkErrorPersistenceWriter_PersistsHandledBookingErrorRecord()
    {
        using var dbContext = CreateDbContext();
        var mapping = new BookingExceptionMapper().TryMap(new JsonException("Bad JSON"));

        Assert.NotNull(mapping);

        var record = new ErrorRecordBuilder().Build(mapping!.MappingResult);
        var writer = new EntityFrameworkErrorPersistenceWriter<BookingDbContext>(dbContext);

        await writer.WriteAsync(record);

        var persisted = await dbContext.Set<ErrorRecordEntity>().SingleAsync();
        Assert.Equal("InvalidJson", persisted.Code);
        Assert.Equal("Validation", persisted.Category);
        Assert.Equal("Warning", persisted.Severity);
        Assert.Equal("Request body must be valid JSON with supported date/time values.", persisted.Message);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }
}
