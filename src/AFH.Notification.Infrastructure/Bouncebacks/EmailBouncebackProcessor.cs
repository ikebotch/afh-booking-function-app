using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Bouncebacks;

public sealed class EmailBouncebackProcessor : INotificationBouncebackProcessor
{
    private readonly EmailBouncebackParser _parser;
    private readonly INotificationBouncebackStore _store;
    private readonly ILogger<EmailBouncebackProcessor> _logger;

    public EmailBouncebackProcessor(
        EmailBouncebackParser parser,
        INotificationBouncebackStore store,
        ILogger<EmailBouncebackProcessor> logger)
    {
        _parser = parser;
        _store = store;
        _logger = logger;
    }

    public async Task<NotificationBouncebackResult> ProcessWebhookPayloadAsync(string payload, CancellationToken ct)
    {
        var (result, bouncebacks) = _parser.Parse(payload);

        if (!result.IsSuccess)
        {
            return result;
        }
        
        // Return early for validation events
        if (!string.IsNullOrEmpty(result.ValidationResponse))
        {
            return result;
        }

        foreach (var bounceback in bouncebacks)
        {
            try
            {
                await _store.RecordBouncebackAsync(bounceback, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record bounceback for ProviderMessageId: {ProviderMessageId}", bounceback.ProviderMessageId);
            }
        }

        return new NotificationBouncebackResult(true, null, bouncebacks.Count);
    }
}
