using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Functions.V1.Bookings;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AFH.Booking.Tests;

public sealed class ConfirmHoldFunctionTests
{
    [Fact]
    public async Task Run_AcceptsEmptyBody()
    {
        var service = new StubConfirmBookingService();
        var sut = new ConfirmHoldFunction(service);
        var request = TestHttpRequestData.Create();
        ConfigureSerializer(request);

        var response = await sut.Run(request, "hold-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(service.LastCommand);
        Assert.Equal("hold-1", service.LastCommand!.HoldId);
        Assert.Equal("hold-1", service.LastCommand.BookingId);
        Assert.Null(service.LastCommand.Notes);
    }

    [Fact]
    public async Task Run_AcceptsEmptyObjectBody()
    {
        var service = new StubConfirmBookingService();
        var sut = new ConfirmHoldFunction(service);
        var request = TestHttpRequestData.Create();
        ConfigureSerializer(request);
        await using var writer = new StreamWriter(request.Body, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync("""{}""");
        await writer.FlushAsync();
        request.Body.Position = 0;

        var response = await sut.Run(request, "hold-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(service.LastCommand);
        Assert.Equal("hold-1", service.LastCommand!.HoldId);
        Assert.Equal("hold-1", service.LastCommand.BookingId);
        Assert.Null(service.LastCommand.Notes);
    }

    [Fact]
    public async Task Run_AcceptsOmittedNotes()
    {
        var service = new StubConfirmBookingService();
        var sut = new ConfirmHoldFunction(service);
        var request = TestHttpRequestData.Create();
        ConfigureSerializer(request);
        await using var writer = new StreamWriter(request.Body, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync("""{"bookingId":"hold-1"}""");
        await writer.FlushAsync();
        request.Body.Position = 0;

        var response = await sut.Run(request, "hold-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(service.LastCommand);
        Assert.Equal("hold-1", service.LastCommand!.HoldId);
        Assert.Equal("hold-1", service.LastCommand.BookingId);
        Assert.Null(service.LastCommand.Notes);
    }

    [Fact]
    public async Task Run_AcceptsNullNotes()
    {
        var service = new StubConfirmBookingService();
        var sut = new ConfirmHoldFunction(service);
        var request = TestHttpRequestData.Create();
        ConfigureSerializer(request);
        await using var writer = new StreamWriter(request.Body, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync("""{"bookingId":"hold-1","notes":null}""");
        await writer.FlushAsync();
        request.Body.Position = 0;

        var response = await sut.Run(request, "hold-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(service.LastCommand);
        Assert.Null(service.LastCommand!.Notes);
    }

    [Fact]
    public async Task Run_ReturnsBadRequestForInvalidNonEmptyJson()
    {
        var service = new StubConfirmBookingService();
        var sut = new ConfirmHoldFunction(service);
        var request = TestHttpRequestData.Create();
        ConfigureSerializer(request);
        await using var writer = new StreamWriter(request.Body, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync("""{"notes":""");
        await writer.FlushAsync();
        request.Body.Position = 0;

        var response = await sut.Run(request, "hold-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    private static void ConfigureSerializer(TestHttpRequestData request)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<WorkerOptions>>(Options.Create(new WorkerOptions
        {
            Serializer = new JsonObjectSerializer()
        }));

        request.FunctionContext.InstanceServices = services.BuildServiceProvider();
    }

    private sealed class StubConfirmBookingService : IConfirmBookingService
    {
        public ConfirmBookingCommand? LastCommand { get; private set; }

        public Task<Result<ConfirmBookingResponse>> HandleAsync(ConfirmBookingCommand cmd, CancellationToken ct)
        {
            LastCommand = cmd;
            return Task.FromResult(Result<ConfirmBookingResponse>.Ok(new ConfirmBookingResponse
            {
                BookingId = cmd.HoldId,
                SlotId = "slot-1",
                TransactionId = "tx-1",
                TransactionRef = "TRX-1",
                Status = "Confirmed",
                LifecycleState = "Booked"
            }));
        }
    }
}