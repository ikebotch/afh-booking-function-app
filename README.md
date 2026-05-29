# AFH Booking Service

## What It Owns
- Booking orchestration and booking operational state.
- Hold, confirm, cancel, rearrange, approval, and projection workflows.
- Internal calls to the location service for in-person adviser search.
- Internal calls to the calendar service for appointment and subscription work.

## Local Setup
1. Copy `src/AFH.Booking.Functions/local.settings.template.json` to `src/AFH.Booking.Functions/local.settings.json`.
2. Fill in the required values:
   `BookingDb:ConnectionString`, `Calendars:BaseUrl`, `Calendars:FunctionKey`, `Calendars:InternalToken`, `LocationService:BaseUrl`, `LocationService:FunctionKey`, `LocationService:InternalToken`, `InternalApiAuth:Token`.
3. Fill in optional integrations only if you are using them locally:
   `Acs:*`, `Leads:*`, `Notifications:*`, `AdviserDirectory:*`, `XPlan:*`, `DomainUserAuth:*`.
4. Keep `InternalApiAuth:AllowAnonymousInDevelopment=true` only for local development.

## Local Settings Conventions
- Shared internal bearer auth uses `InternalApiAuth:Token` on the receiving service.
- Outbound service calls use `<ServiceSection>:BaseUrl`, `<ServiceSection>:FunctionKey`, and `<ServiceSection>:InternalToken` where that downstream service requires both function auth and bearer auth.
- Booking keeps existing section names such as `Calendars:*` and `LocationService:*` because those are the active bound options in code. This pass did not rename them because that would be a breaking config change.

## Internal Auth
- Booking-to-calendar, booking-to-location, and booking-to-ACS internal calls now use function-specific keys plus `Authorization: Bearer <token>`.
- Booking internal calendar routes also require function-level auth plus the shared bearer token via `InternalApiAuth:*`.
- Do not use the master key for routine backend-to-backend traffic.

## Domain User Auth
- Domain users sign in with Microsoft Entra ID / Azure AD from the Vue app.
- Vue only handles sign-in and token acquisition. It does not mint custom tokens or act as the permission source of truth.
- Booking validates Entra bearer tokens server-side, enforces tenant and email-domain rules, and resolves application roles and capabilities in the backend.
- `GET /api/v1/me` is the frontend bootstrap endpoint for signed-in domain users.
- `/me` now returns `403` for signed-in users who do not map to a Booking domain role.
- Frontends should use the `/me` response for UX shaping only; Booking remains the source of truth for authorization and policy checks.
- Configure Entra validation and role mapping with `DomainUserAuth:*` in `src/AFH.Booking.Functions/local.settings.template.json`.

## Client Self-Service Booking Routes
- Client self-service journeys must call the `/api/v1/self-service/bookings/{bookingId}` routes, not the internal/admin booking routes.
- The secure client access token is opaque and server-validated. For frontend links, pass it as the `token` query string value.
- Invalid, missing, or expired client tokens return `401`. A valid token for a different booking returns `403`.
- Implemented Sprint 2 self-service routes:
  - `GET /api/v1/self-service/bookings/{bookingId}?token={token}` views client-facing booking details.
  - `POST /api/v1/self-service/bookings/{bookingId}/cancel?token={token}` cancels the booking for the client journey.
  - `POST /api/v1/self-service/bookings/{bookingId}/rearrangement/options?token={token}` returns UTC rearrangement options.
  - `POST /api/v1/self-service/bookings/{bookingId}/rearrange?token={token}` rearranges the booking from a selected slot.
- Booking details responses include `viewBookingUrl`, `cancelBookingUrl`, and `rescheduleBookingUrl` when self-service links can be generated.
- After rearrange, the replacement booking requires its own new token. Do not reuse the old booking token for the new booking.

## Build And Test
- `dotnet test AFH.BookingService.sln`
- `tests/AFH.Booking.Tests` now covers the current lifecycle sequencing and governance paths in the active repo state.

## Notification Architecture
- Applications publish notification intent. Booking lifecycle owns when notification intent is created.
- Booking owns notification policy/routing decisions for Booking events.
- Notification bounded context owns execution, templates, queueing, rendered-message audit, delivery audit, and bounceback processing.
- Email and SMS are supported delivery channels; Push remains a future channel.
- Notification templates are stored in `NotificationTemplates` through EF migrations for `NotificationDbContext`; embedded `.txt` templates are retained as a one-release fallback.
- Bouncebacks are provider feedback handled explicitly by Notification Infrastructure.
- Contact-centre copy is a Booking recipient policy row, not a column on `NotificationOutbox` or `NotificationDispatches`.
- Hold notifications are disabled by default and must remain configuration-gated unless the business explicitly approves them.
- Both notification paths are intentionally active during the Sprint 7 transition.
- Shared-host transition:
  - `AFH.Booking.Function` temporarily hosts Booking HTTP functions plus Notification-owned inbound, dispatch, and bounceback functions.
  - Transitional Notification functions are grouped under `Functions/V1/Notifications/Inbound`, `Functions/V1/Notifications/Dispatch`, and `Functions/V1/Notifications/Bouncebacks`.
  - The shared Function host may reference both Booking and Notification modules for composition; Booking Application/Infrastructure must still depend only on `AFH.Notification.Contract` abstractions and must not reference `AFH.Notification.Infrastructure`.
- Source-to-Notification boundary:
  - Source applications submit the source-neutral `NotificationRequested` contract by HTTP or Azure Service Bus.
  - HTTP path: source app -> `HttpNotificationPublisher` -> `POST /api/v1/notifications/requests` -> `NotificationRequestIngestionService`.
  - Service Bus path: source app -> `ServiceBusNotificationPublisher` -> `notification-requests` topic/queue -> Notification Service Bus consumer -> `NotificationRequestIngestionService`.
  - HTTP is the current/default near-term transport. Service Bus is available for asynchronous decoupling and multi-source integration.
  - Both inbound transports call the same shared ingestion service; neither sends email synchronously.
- Notification admin/status endpoints are hosted in the shared Function app with Function-level authorization during the transition:
  - `GET /api/v1/notifications/templates`
  - `GET /api/v1/notifications/templates/{id}`
  - `GET /api/v1/notifications/templates/by-key/{templateKey}/versions/{templateVersion}/channels/{channel}`
  - `POST /api/v1/notifications/templates`
  - `PUT /api/v1/notifications/templates/{id}`
  - `PATCH /api/v1/notifications/templates/{id}/activate`
  - `PATCH /api/v1/notifications/templates/{id}/deactivate`
  - `POST /api/v1/notifications/templates/preview`
  - `GET /api/v1/notifications/requests`
  - `GET /api/v1/notifications/requests/{id}`
  - `GET /api/v1/notifications/dispatches/{id}`
  - `GET /api/v1/notifications/message-logs/{id}`
  - `POST /api/v1/notifications/requests/{id}/requeue`
  - `POST /api/v1/notifications/requests/{id}/dead-letter`
  - `POST /api/v1/notifications/requests/{id}/mark-failed`
  - Broad request/dispatch list responses must not expose rendered body content. Full rendered subject/body is available only from the specific message-log endpoint and must remain admin/internal only.
- Internal Notification dispatch:
  - After ingestion, `NotificationRequested` -> `NotificationOutbox` -> Azure Queue message containing only `outboxId` -> `SendNotificationQueueTrigger` -> `NotificationService` -> `NotificationTemplates` -> Email/SMS delivery gateway -> `NotificationDispatches` -> `NotificationMessageLogs`.
  - Final service split target: Booking resolves policy/recipients/template/channel, publishes `NotificationRequested`, and stops; Notification consumes that request, creates `NotificationOutbox`, and uses the internal OutboxId queue.
  - `NotificationOutbox` is Notification-owned. The Azure Queue `outboxId` message is internal to the Notification service, not the Booking-to-Notification boundary.
- Retained compatibility path:
  - `ApprovalNotificationService`, queued delivery audit, and bouncebacks continue to use `NotificationDispatches`.
  - `NotificationDispatches` remains active for delivery audit and bounceback correlation. Do not drop it yet.
- The new path uses a hybrid dispatch model.
  - SQL stores the full notification payload, processing state, idempotency key, and audit metadata in `NotificationOutbox`.
  - Azure Storage Queue is used only as an internal Notification-service wake-up signal and contains only `outboxId`.
  - Transitional flow: Booking lifecycle event -> `NotificationOutbox` row in SQL -> Azure Queue message containing only `outboxId` -> queue trigger loads full payload from SQL -> `NotificationService` dispatches through Graph -> SQL status is updated.
  - Azure Queue does not contain sensitive notification data. Do not put rendered subject/body, render data, provider metadata content, or full `NotificationRequested` payloads in Azure Queue, Azure Table storage, or external queue payloads.
  - SQL remains the source of truth.
  - No Event Grid subscription is needed for Azure Queue sending; the queue trigger listens automatically.
  - Queue settings are required: `Notifications__Queue__QueueName` and `Notifications__Queue__ConnectionString`.
  - `Notifications__Queue__ConnectionString` is an Azure Storage Account connection string, not the Booking SQL connection string.
  - Prefer Key Vault/App Settings for the storage connection string.
  - The function app identity or connection string must be allowed to create the queue if `CreateIfNotExistsAsync` remains enabled.
  - Built-in poison queue behavior exists through Azure Functions for retry-exhausted queue messages; invalid persisted payloads are marked `DeadLettered` by the trigger and should be monitored from `NotificationOutbox`.
  - If Azure enqueue succeeds but marking the outbox row `Queued` fails, the publisher throws and the row remains `Pending`; operations should repair/requeue those rows until a dedicated requeue function is added.
- Queued email delivery sends via Microsoft Graph when `Notifications:Email:ProviderName=Graph` and valid Graph settings are configured.
- `Notifications:Email:ProviderName=Composed` keeps the non-production composed behavior and returns `NonProductionComposed`.
- Queued SMS delivery sends through Azure Communication Services or Twilio when `Notifications:Sms:Enabled=true` and the matching provider settings are configured.
- `Notifications:Sms:ProviderName=Composed` keeps non-production composed behavior and does not send real SMS.
- SMS templates are DB-backed `NotificationTemplates` rows with `Channel=Sms`, `BodyTemplate` required, no subject required, and `ContentType=text/plain`.
- SMS rendered bodies are stored only in SQL `NotificationMessageLogs.Body`; application logs must include metadata/length only.
- Production deployment requires Key Vault/App Settings for Graph credentials and mailbox permissions for SendMail.
- Contact-centre recipients are resolved by Booking through DB-backed organisation assignments.
- Bounceback auditing persists `EmailBounceEvents` and correlates with the unified `NotificationDispatches` delivery-attempt audit table. `NotificationOutbox` remains job-level; `NotificationDispatches` remains recipient/channel/provider attempt-level.
- Rendered message audit is stored in `NotificationMessageLogs`, not `NotificationDispatches`.
  - `NotificationMessageLogs` is Notification-owned SQL data and is allowed to contain sensitive rendered notification content because SQL is the approved sensitive store for this flow.
  - It stores exact rendered subject/body, template key/version, channel, recipient metadata, dispatch id, outbox id, render data JSON, body hash, and created timestamp.
  - Treat `NotificationMessageLogs` as sensitive audit data. Do not log rendered bodies to application logs and do not include rendered content in queue messages, Azure Table storage, or provider correlation metadata.
  - Storing rendered body content is a separate business/compliance decision from lightweight delivery metadata. `NotificationDispatches` must remain lightweight for normal dispatch queries.
- Table ownership is logical even though the tables live in the same Booking SQL database:
  - `NotificationDbContext` owns `NotificationOutbox`, `NotificationDispatches`, `NotificationMessageLogs`, `EmailBounceEvents`, and `NotificationTemplates`.
  - `BookingDbContext` owns `BookingNotificationRules`, `BookingNotificationRuleChannels`, and `BookingNotificationRuleRecipients`.
  - Booking policy rows reference notification templates by `TemplateKey` and `TemplateVersion`; Booking does not own template subject/body content.
  - Do not add recipient policy columns such as `SendToClient`, `SendToAdviser`, or `CopyContactCentre`; future recipient types are rows in `BookingNotificationRuleRecipients`.
- `NotificationDispatches` is source-neutral for new queued delivery audit:
  - New queued writes populate neutral columns such as `SourceApplication`, `SourceReferenceType`, `SourceReferenceId`, `NotificationType`, `RecipientType`, `RecipientEmail`, `RecipientMobile`, `Channel`, `ProviderName`, `ProviderMessageId`, `TemplateKey`, `TemplateVersion`, `Status`, and `FailureDetails`.
  - Legacy Booking-specific columns (`BookingId`, `TransactionId`, `TransactionRef`, `LifecycleEventId`, `EventType`, `SmsRequested`, `EmailRequested`, `SmsStatus`, `EmailStatus`, `OutcomeCode`, `RecipientPhone`, and `TemplateName`) are retained only for compatibility with historical rows/reporting and older audit flows.
  - `MessageSubject` and `MessageBody` are compatibility columns only; new queued writes do not populate them as the primary rendered-content store.
  - `Status` is the neutral delivery-attempt status. `OutcomeCode`, `EmailStatus`, and `SmsStatus` are temporary compatibility fields and should be removed only after retention/reporting review.
- **Wording Note:** Current live lifecycle wording uses `Rearranged`, whereas notification template naming uses `Rescheduled`. Do not change wording in Sprint 7 unless product confirms it.

## Notification Configuration Split
- Legacy Booking self-service link options are flat keys under `Notifications`:
  - `Notifications:ClientPortalBaseUrl`
- Booking-side outbound notification publishing uses `Booking:Notifications:Http:*`:
  - `Booking:Notifications:Http:BaseUrl`
  - `Booking:Notifications:Http:RequestPath`
  - `Booking:Notifications:Http:TimeoutSeconds`
  - `Booking:Notifications:Http:FunctionKey`
    - Required when the receiving notification endpoint uses Azure Functions `AuthorizationLevel.Function`; sent as the `code` query value.
  - `Booking:Notifications:Http:InternalToken`
    - Required internal token for the notification API; sent as `Authorization: Bearer <InternalToken>`.
- Notification-side source publishing uses `Notifications:Integration:*`:
  - `Notifications:Integration:Transport` supports `ServiceBus` and transitional/local `InProcess`.
  - `Notifications:Integration:ServiceBus:FullyQualifiedNamespace`
  - `Notifications:Integration:ServiceBus:ConnectionString`
  - `Notifications:Integration:ServiceBus:TopicName`
  - `Notifications:Integration:ServiceBus:QueueName`
- Notification-side inbound receiving uses `Notifications:Inbound:*`:
  - `Notifications:Inbound:ServiceBus:Enabled`
  - `Notifications:Inbound:ServiceBus:FullyQualifiedNamespace`
  - `Notifications:Inbound:ServiceBus:ConnectionString`
  - `Notifications:Inbound:ServiceBus:TopicName`
  - `Notifications:Inbound:ServiceBus:SubscriptionName`
  - `Notifications:Inbound:ServiceBus:QueueName`
- Notification internal queue dispatch uses `Notifications:Queue:*`:
  - `Notifications:Queue:QueueName`
  - `Notifications:Queue:ConnectionString`
- Delivery/provider options remain under nested Notification infrastructure settings:
  - `Notifications:Email:Enabled`
  - `Notifications:Email:ProviderName`
  - `Notifications:Email:ProviderName` allowed values are `Composed` and `Graph`; `SendGrid` is an email-only future option if introduced later under `Notifications:Email:SendGrid:*`.
  - `Notifications:Email:Graph:*`
  - `Notifications:Sms:Enabled`
  - `Notifications:Sms:ProviderName`
  - `Notifications:Sms:ProviderName` allowed values are `Composed`, `AzureCommunicationServices`, and `Twilio`.
  - `Notifications:Sms:DefaultSender`
  - `Notifications:Sms:AzureCommunicationServices:ConnectionString`
  - `Notifications:Sms:AzureCommunicationServices:Endpoint`
  - `Notifications:Sms:AzureCommunicationServices:UseManagedIdentity`
  - `Notifications:Sms:AzureCommunicationServices:FromPhoneNumber`
  - `Notifications:Sms:AzureCommunicationServices:DeliveryReportEnabled`
  - `Notifications:Sms:Twilio:AccountSid`
  - `Notifications:Sms:Twilio:AuthToken`
  - `Notifications:Sms:Twilio:FromPhoneNumber`
  - `Notifications:Sms:Twilio:MessagingServiceSid`
- Azure Functions-safe app settings use double underscores, for example `Notifications__Integration__Transport`, `Notifications__Integration__Http__BaseUrl`, `Notifications__Inbound__ServiceBus__Enabled`, and `Notifications__Queue__QueueName`.
- Keep `Notifications:ClientPortalBaseUrl` configured until Booking self-service link generation moves to a dedicated options section.

## Microsoft Graph Email Delivery
- Configure queued notification email delivery with:
  - `Notifications:Email:Enabled=true`
  - `Notifications:Email:ProviderName=Graph`
  - `Notifications:Email:Graph:UseManagedIdentity`
  - `Notifications:Email:Graph:TenantId`
  - `Notifications:Email:Graph:ClientId`
  - `Notifications:Email:Graph:ClientSecret`
  - `Notifications:Email:Graph:SenderMailbox`
- Prefer Managed Identity with `Notifications:Email:Graph:UseManagedIdentity=true`.
- Use `ClientSecretCredential` only with `Notifications:Email:Graph:UseManagedIdentity=false`.
- Secrets belong in Key Vault/App Settings only; do not commit real secrets to source-controlled configuration.
- The Graph app registration or managed identity must have permission to send as/from `Notifications:Email:Graph:SenderMailbox`; deployment requires admin consent and the appropriate Microsoft Graph SendMail permissions/mailbox access.
- Database templates remain plain text for now, so Graph sends plain text bodies. HTML/multipart delivery is intentionally out of scope.
- Microsoft Graph `sendMail` returns `202 Accepted` without a provider message id. The service stores an internal provider correlation id as `ProviderMessageId` for tracing successful sends.
- Because the Graph `ProviderMessageId` is internal rather than a Graph-generated message id, queued Graph dispatch rows are ready for bounceback correlation by stored provider correlation id, but production provider metadata should be verified end-to-end before relying on Graph bouncebacks operationally.
- Legacy direct Booking email sending has been removed from active runtime paths; approval/audit/bounceback compatibility remains through `NotificationDispatches`.

## SMS Delivery
- Configure queued notification SMS delivery with:
  - `Notifications:Sms:Enabled=true`
  - `Notifications:Sms:ProviderName=AzureCommunicationServices` or `Twilio`
  - ACS: `Notifications:Sms:AzureCommunicationServices:ConnectionString`, `FromPhoneNumber`, and optional `DeliveryReportEnabled`
  - Twilio: `Notifications:Sms:Twilio:AccountSid`, `AuthToken`, and either `FromPhoneNumber` or `MessagingServiceSid`
- SMS recipients must have an E.164 mobile number where possible, for example `+447700900000`.
- If SMS is disabled, SMS attempts are recorded as configured off and do not block enabled Email delivery.
- SMS delivery reports/callbacks are not implemented in Sprint 7. Keep `EmailBounceEvents` email-focused; future provider feedback should use a generalized provider-event model rather than forcing SMS into email bounce tables.
- ACS connection-string SMS sending is implemented. ACS managed identity settings are present for future deployment hardening, but managed identity sending remains a follow-up.

## Notification Queue Settings
- Hybrid notification dispatch requires:
  - `Notifications__Queue__QueueName=notifications-send`
  - `Notifications__Queue__ConnectionString=<Azure Storage Account connection string>`

## Notification Transition Follow-Ups
- Move Notification functions from the shared `AFH.Booking.Function` host into a future `AFH.Notification.Function` deployment after infra is ready.
- Keep source apps on HTTP or Service Bus at the logical boundary before the split; remove the transitional `InProcess` publisher/hosting option after the split.
- Verify Graph/provider bounceback metadata against queued delivery audit in production-like integration testing.
- Decide whether the manual `Bookings_SendNotification` endpoint should remain internal/admin-only or be retired after cutover.
- Monitor the removed direct sender replacement in production cutover and keep rollback guidance in release notes.
- Decide retention and schema cleanup for `NotificationDispatches` after migration and data-retention review.

## SQL Migration Note
- Lifecycle and Outlook-governance changes now require database schema support for lifecycle audit tables and `OperationalIssues`.
- Create and apply an EF migration from the infrastructure project before deploying to shared environments.
- `NotificationOutbox` schema is deployed through EF migrations for `NotificationDbContext`.
- `NotificationTemplates`, `NotificationDispatches`, `NotificationMessageLogs`, and `EmailBounceEvents` are notification-owned and mapped by `NotificationDbContext`.
- Booking notification policy tables are Booking-owned and mapped by `BookingDbContext`.
- Do not manually maintain `notification-outbox.sql` as the source of truth.
- Treat that migration as required infra work for this backend phase.
- Current `NotificationOutboxStore` persistence tests depend on a local SQL Server instance; add CI-backed SQL integration coverage before production cutover.

## Reconciliation And Smoke Tests
- Manual downstream/XPlan reconciliation is available via `POST /api/v1/admin/downstream-updates/reconcile`.
- This retries stale `Pending` and `Failed` downstream update rows explicitly instead of silently masking partial failures.
- Recommended non-prod smoke journeys:
  - create booking
  - remote booking with ACS meeting link
  - cancel booking
  - rearrange booking
  - calendar notification intake
  - downstream/XPlan update publish and manual reconcile
- Required env/config should include Booking DB, shared internal auth token, calendar/location/ACS base URLs and function keys, notification settings, and XPlan base URL/API key when that path is enabled.

## More Detail
- See `docs/backend-architecture.md` for service boundaries, endpoint classification, configuration, and timezone handling.
- The same document now also covers lifecycle SQL/audit tables, orchestration sequencing, and deferred cancellation/rearrangement work for later phases.
