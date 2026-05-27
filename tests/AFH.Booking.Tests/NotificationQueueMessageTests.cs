using System.Reflection;
using System.Text.Json;
using AFH.Notification.Application.Models;
using Xunit;

namespace AFH.Booking.Tests;

public sealed class NotificationQueueMessageTests
{
    [Fact]
    public void NotificationQueueMessage_HasOnlyOutboxId()
    {
        var property = Assert.Single(typeof(NotificationQueueMessage).GetProperties(BindingFlags.Instance | BindingFlags.Public));

        Assert.Equal(nameof(NotificationQueueMessage.OutboxId), property.Name);
        Assert.Equal(typeof(Guid), property.PropertyType);
    }

    [Fact]
    public void NotificationQueueMessage_SerializesOnlyOutboxId()
    {
        var message = new NotificationQueueMessage { OutboxId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") };

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("""{"outboxId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}""", json);
        Assert.DoesNotContain("SourceApplication", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotificationType", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BookingId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HoldId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TransactionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipient", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PayloadJson", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("body", json, StringComparison.OrdinalIgnoreCase);
    }
}
