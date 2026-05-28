# Notification Sprint 7 PR Notes

## Completion State

- Notification is logically central but physically hosted in `AFH.Booking.Function` for this release.
- Source apps submit the shared `NotificationRequested` contract over HTTP or Service Bus.
- HTTP and Service Bus inbound paths share `NotificationRequestIngestionService`.
- Notification owns `NotificationOutbox`, `NotificationTemplates`, `NotificationDispatches`, `NotificationMessageLogs`, and `EmailBounceEvents`.
- Booking owns `BookingNotificationRules`, `BookingNotificationRuleChannels`, and `BookingNotificationRuleRecipients`.
- Internal Azure Storage Queue messages contain only `outboxId`.
- SQL is the approved sensitive store for payload JSON and exact rendered message content.
- `NotificationDispatches` remains lightweight delivery metadata.
- `NotificationMessageLogs` stores exact rendered subject/body and is sensitive.
- Embedded file templates remain only as a temporary one-release fallback while DB-backed templates are adopted.
- Email providers are `Graph` and non-production `Composed`; `SendGrid` is email-only if introduced later.
- SMS providers are `AzureCommunicationServices`, `Twilio`, and non-production `Composed`.
- SMS rendered bodies are stored in SQL `NotificationMessageLogs`, never queue messages or application logs.

## Admin Surface

- Template CRUD and activation endpoints are under `/api/v1/notifications/templates`.
- Template preview is `POST /api/v1/notifications/templates/preview` and does not send, enqueue, or audit a notification.
- Status/audit endpoints expose request, dispatch, and specific message-log lookup paths.
- Retry operations are `requeue`, `dead-letter`, and `mark-failed` on `/api/v1/notifications/requests/{id}`.

## Deployment Prerequisites

- Configure `Notifications:Integration:*` for source-side publishing.
- Configure `Notifications:Inbound:ServiceBus:*` only when Service Bus receiving is enabled.
- Configure `Notifications:Queue:*` for the internal Azure Storage Queue.
- Configure `Notifications:Email:*` and Graph credentials/mailbox permissions before production email dispatch.
- Configure `Notifications:Sms:*` plus either ACS or Twilio provider settings before production SMS dispatch.
- Apply Notification EF migrations for outbox, templates, dispatch/message audit, and bounce events.

## SMS Follow-Ups

- ACS connection-string SMS sending is implemented; ACS managed identity sending remains future work.
- SMS provider delivery report/callback ingestion is not implemented in Sprint 7.
- Use a future generalized provider-event table for SMS feedback rather than overloading `EmailBounceEvents`.

## Future Split

- Move Notification functions into a new `AFH.Notification.Function` deployable.
- Keep source apps using HTTP or Service Bus only.
- Remove transitional in-process publisher/hosting once the physical split is complete.
