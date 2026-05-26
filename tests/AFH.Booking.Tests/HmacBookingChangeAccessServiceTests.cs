using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Bookings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AFH.Booking.Tests;

public class HmacBookingChangeAccessServiceTests
{
    private readonly Mock<IHostEnvironment> _hostEnvMock = new();
    private readonly Mock<IBookingHoldRepository> _holdsMock = new();
    private readonly Mock<IBookingSlotRepository> _slotsMock = new();
    private readonly Mock<IBookingTransactionRepository> _txMock = new();
    
    private readonly BookingChangeAccessOptions _options = new()
    {
        SigningKey = "super-secret-test-key-which-needs-to-be-long-enough",
        DefaultTokenValidityDays = 30
    };

    private HmacBookingChangeAccessService CreateService()
    {
        return new HmacBookingChangeAccessService(
            Options.Create(_options),
            _hostEnvMock.Object,
            _holdsMock.Object,
            _slotsMock.Object,
            _txMock.Object,
            NullLogger<HmacBookingChangeAccessService>.Instance);
    }

    private T CreateDomainObject<T>(Action<T> configure) where T : class
    {
        var obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
        configure(obj);
        return obj;
    }

    private void SetProperty<T>(T obj, string propertyName, object value)
    {
        var prop = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value, null);
        }
        else if (prop != null)
        {
            // For private setters, get the backing field or use GetSetMethod
            var setter = prop.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(obj, new[] { value });
            }
            else
            {
                // Try backing field e.g. <Id>k__BackingField
                var field = typeof(T).GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(obj, value);
            }
        }
    }

    private void SetupActiveBooking(string bookingId, string slotId, string txId, string txRef)
    {
        var hold = CreateDomainObject<BookingHold>(h => 
        {
            SetProperty(h, "Id", bookingId);
            SetProperty(h, "SlotId", slotId);
            SetProperty(h, "TransactionId", txId);
        });

        var slot = CreateDomainObject<BookingSlot>(s => 
        {
            SetProperty(s, "Id", slotId);
            SetProperty(s, "TransactionId", txId);
        });

        var tx = CreateDomainObject<BookingTransaction>(t => 
        {
            SetProperty(t, "Id", txId);
            SetProperty(t, "TransactionRef", txRef);
        });

        _holdsMock.Setup(h => h.GetAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        _slotsMock.Setup(s => s.GetAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        _txMock.Setup(t => t.GetAsync(txId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);
    }

    [Fact]
    public async Task ValidToken_Succeeds()
    {
        var service = CreateService();
        SetupActiveBooking("b-1", "s-1", "tx-1", "ref-1");

        var tokenResult = await service.GenerateClientTokenAsync("b-1", default);
        Assert.True(tokenResult.IsSuccess);

        var validateResult = await service.ValidateClientTokenAsync("b-1", tokenResult.Value, default);
        Assert.True(validateResult.IsSuccess);
        Assert.Equal("ref-1", validateResult.Value.TransactionRef);
    }

    [Fact]
    public async Task AlteredToken_FailsCleanly()
    {
        var service = CreateService();
        SetupActiveBooking("b-1", "s-1", "tx-1", "ref-1");

        var tokenResult = await service.GenerateClientTokenAsync("b-1", default);
        var alteredToken = tokenResult.Value + "tamper";

        var validateResult = await service.ValidateClientTokenAsync("b-1", alteredToken, default);
        Assert.False(validateResult.IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, validateResult.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_FailsCleanly()
    {
        var service = CreateService();
        SetupActiveBooking("b-1", "s-1", "tx-1", "ref-1");
        
        // Generate an expired token by forcing the envelope logic
        var expiredEnvelope = new BookingChangeAccessTokenEnvelope("b-1", LifecycleActors.Client, DateTimeOffset.UtcNow.AddDays(-1), TransactionRef: "ref-1");
        var expiredToken = HmacBookingChangeAccessService.CreateToken(expiredEnvelope, _options.SigningKey);

        var validateResult = await service.ValidateClientTokenAsync("b-1", expiredToken, default);
        Assert.False(validateResult.IsSuccess);
        Assert.Equal(HttpStatusCode.Unauthorized, validateResult.StatusCode);
        Assert.Contains("expired", validateResult.ErrorMessage);
    }

    [Fact]
    public async Task WrongBookingId_FailsCleanly()
    {
        var service = CreateService();
        SetupActiveBooking("b-1", "s-1", "tx-1", "ref-1");
        SetupActiveBooking("b-2", "s-2", "tx-2", "ref-2");

        var tokenResult = await service.GenerateClientTokenAsync("b-1", default);
        
        // Attempt to use token for b-1 on b-2
        var validateResult = await service.ValidateClientTokenAsync("b-2", tokenResult.Value, default);
        Assert.False(validateResult.IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, validateResult.StatusCode);
        Assert.Contains("does not match", validateResult.ErrorMessage);
    }

    [Fact]
    public async Task TransactionContinuityMismatch_FailsCleanly()
    {
        var service = CreateService();
        SetupActiveBooking("b-1", "s-1", "tx-1", "ref-1");
        var tokenResult = await service.GenerateClientTokenAsync("b-1", default);

        // Simulate database state changing the transaction ref unexpectedly
        SetupActiveBooking("b-1", "s-1", "tx-1", "ref-changed");

        var validateResult = await service.ValidateClientTokenAsync("b-1", tokenResult.Value, default);
        Assert.False(validateResult.IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, validateResult.StatusCode);
        Assert.Contains("transaction does not match", validateResult.ErrorMessage);
    }

    [Fact]
    public async Task Reschedule_RejectsOldToken_And_AcceptsNewToken()
    {
        var service = CreateService();
        
        // Initial Booking
        SetupActiveBooking("b-old", "s-old", "tx-old", "shared-ref");
        var oldTokenResult = await service.GenerateClientTokenAsync("b-old", default);

        // Rearrange happens -> creates b-new, deletes b-old
        SetupActiveBooking("b-new", "s-new", "tx-new", "shared-ref");
        _holdsMock.Setup(h => h.GetAsync("b-old", It.IsAny<CancellationToken>())).ReturnsAsync((BookingHold?)null); // old hold deleted

        // 1. Old token fails against new booking
        var validateOldOnNew = await service.ValidateClientTokenAsync("b-new", oldTokenResult.Value, default);
        Assert.False(validateOldOnNew.IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, validateOldOnNew.StatusCode);
        Assert.Contains("does not match", validateOldOnNew.ErrorMessage);

        // 2. Old token fails against old booking (because hold is deleted)
        var validateOldOnOld = await service.ValidateClientTokenAsync("b-old", oldTokenResult.Value, default);
        Assert.False(validateOldOnOld.IsSuccess);
        Assert.Equal(HttpStatusCode.NotFound, validateOldOnOld.StatusCode);
        Assert.Contains("was not found", validateOldOnOld.ErrorMessage);

        // 3. New token is issued and accepted for the replacement booking
        var newTokenResult = await service.GenerateClientTokenAsync("b-new", default);
        Assert.True(newTokenResult.IsSuccess);

        var validateNew = await service.ValidateClientTokenAsync("b-new", newTokenResult.Value, default);
        Assert.True(validateNew.IsSuccess);
        Assert.Equal("shared-ref", validateNew.Value.TransactionRef);
    }
}
