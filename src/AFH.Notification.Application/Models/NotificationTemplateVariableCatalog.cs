namespace AFH.Notification.Application.Models;

public static class NotificationTemplateVariableCatalog
{
    public static readonly IReadOnlyList<string> BookingConfirmed =
    [
        "eventId",
        "slotId",
        "adviserId",
        "adviserName",
        "startUtc",
        "endUtc",
        "transactionRef",
        "bookingId",
        "clientName",
        "clientEmail",
        "clientPhone",
        "meetingType",
        "meetingTopic",
        "meetingDate",
        "meetingDateDay",
        "meetingDateTime",
        "meetingMethod",
        "meetingMode",
        "meetingDuration",
        "meetingStatus",
        "date",
        "time",
        "meetingAddress",
        "meetingAddressLine1",
        "meetingAddressLine2",
        "meetingTown",
        "meetingCounty",
        "meetingPostcode",
        "when",
        "joinUrl",
        "joinMeetingLink",
        "whereLine",
        "travelLine",
        "manageBookingLink",
        "manageBookingLinks",
        "viewBookingUrl",
        "cancelBookingUrl",
        "rescheduleBookingUrl",
        "RecipientType",
        "recipientType"
    ];

    public static readonly IReadOnlyList<string> BookingHoldCreated =
    [
        "transactionRef",
        "holdId",
        "clientName",
        "clientEmail",
        "clientPhone",
        "adviserName",
        "meetingType",
        "meetingTopic",
        "meetingDate",
        "meetingDateDay",
        "meetingDateTime",
        "meetingMethod",
        "meetingMode",
        "meetingDuration",
        "meetingStatus",
        "date",
        "time",
        "meetingAddress",
        "meetingAddressLine1",
        "meetingAddressLine2",
        "meetingTown",
        "meetingCounty",
        "meetingPostcode",
        "when",
        "holdExpires",
        "travelLine",
        "companyLine",
        "manageBookingLink",
        "manageBookingLinks",
        "bookingId",
        "RecipientType",
        "recipientType"
    ];

    public static readonly IReadOnlyList<string> BookingCancelled =
    [
        "eventId",
        "bookingId",
        "slotId",
        "adviserId",
        "adviserName",
        "startUtc",
        "endUtc",
        "transactionRef",
        "clientName",
        "clientEmail",
        "clientPhone",
        "greetingName",
        "meetingType",
        "meetingTopic",
        "meetingDate",
        "meetingDateDay",
        "meetingDateTime",
        "meetingMethod",
        "meetingMode",
        "meetingDuration",
        "meetingStatus",
        "date",
        "time",
        "meetingAddress",
        "meetingAddressLine1",
        "meetingAddressLine2",
        "meetingTown",
        "meetingCounty",
        "meetingPostcode",
        "whenLine",
        "locationLine",
        "note",
        "manageBookingLink",
        "manageBookingLinks",
        "contactNumber",
        "contactUsNumber",
        "reasonCode",
        "reasonDetail",
        "RecipientType",
        "recipientType"
    ];

    public static readonly IReadOnlyList<string> BookingRescheduled =
    [
        "eventId",
        "previousBookingId",
        "newBookingId",
        "previousSlotId",
        "newSlotId",
        "adviserName",
        "startUtc",
        "endUtc",
        "clientName",
        "clientEmail",
        "clientPhone",
        "greetingName",
        "bookingId",
        "meetingType",
        "meetingTopic",
        "meetingDate",
        "meetingDateDay",
        "meetingDateTime",
        "meetingMethod",
        "meetingMode",
        "meetingDuration",
        "meetingStatus",
        "date",
        "time",
        "meetingAddress",
        "meetingAddressLine1",
        "meetingAddressLine2",
        "meetingTown",
        "meetingCounty",
        "meetingPostcode",
        "whenLine",
        "locationLine",
        "note",
        "joinUrl",
        "joinMeetingLink",
        "manageBookingLink",
        "manageBookingLinks",
        "RecipientType",
        "recipientType"
    ];

    public static readonly IReadOnlyList<string> AdviserRequestOutcome =
    [
        "RequestId",
        "requestId",
        "BookingId",
        "bookingId",
        "TransactionId",
        "transactionId",
        "TransactionRef",
        "transactionRef",
        "AdviserId",
        "adviserId",
        "Reviewer",
        "reviewer",
        "Outcome",
        "outcome",
        "Status",
        "status",
        "ChangeType",
        "changeType",
        "ReasonCode",
        "reasonCode",
        "ReasonDetail",
        "reasonDetail",
        "DecisionNotes",
        "decisionNotes"
    ];

    public static readonly IReadOnlyList<string> DeliveryFailure =
    [
        "dispatchId",
        "templateKey",
        "templateVersion",
        "channel",
        "recipientType",
        "recipient",
        "providerName",
        "failureReason"
    ];

    public static IReadOnlyList<string> ForLifecycleEvent(string? lifecycleEvent)
    {
        var value = Normalize(lifecycleEvent);
        return value switch
        {
            "bookingconfirmed" or "bookingconfirmedv1" or "bookingconfirmedemail" => BookingConfirmed,
            "bookingreminder" => BookingConfirmed,
            "bookingholdcreated" or "holdcreated" or "bookinghold" => BookingHoldCreated,
            "bookingcancelled" or "cancelled" => BookingCancelled,
            "bookingrearranged" or "bookingrescheduled" or "rearranged" or "rescheduled" => BookingRescheduled,
            "approvalrequested" or "adviserrequestoutcome" or "adviserrequestreviewed" => AdviserRequestOutcome,
            "deliveryfailed" or "deliveryfailure" => DeliveryFailure,
            _ => []
        };
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
