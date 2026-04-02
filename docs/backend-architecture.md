# AFH Backend Architecture

## Service Responsibilities
- Booking service: system of record for booking transactions, holds, confirmations, cancellations, rearrangements, projections, and approval workflows.
- Location service: in-person adviser discovery, filtering, ranking, travel evaluation, adviser reference caching, and policy-driven availability shaping.
- Calendar service: calendar-provider facade for schedule lookup, appointment lifecycle, Graph subscriptions, Graph notification intake, and calendar projection reads.
- ACS service: ACS session and join-link concerns only on the booking path. Booking requests the session/link, then calls Calendar separately for Outlook changes.

## Endpoint Classification
- Public:
  - Booking: health and docs.
  - Location: health and docs.
  - Calendar: health, docs/openapi, and Graph notification callback.
- Domain user:
  - Booking: `GET /api/v1/me` requires a valid Entra bearer token and is used for frontend session bootstrap.
- Internal:
  - Booking: booking orchestration, appointment lifecycle calls, and availability consumption.
  - Location: adviser search, batch search, coverage, and license endpoints.
  - Calendar: appointment, schedule, batch schedule, subscription, and subscription reconcile endpoints.
- Admin:
  - Booking admin endpoints remain function-key protected because they are not part of the backend-to-backend bearer flow in this repo pass.

## Internal Auth Model
- Backend-to-backend calls now use function-specific keys at the Functions boundary plus `Authorization: Bearer <shared internal token>`.
- The bearer token is configured with:
  - Booking inbound: `InternalApiAuth:*`
  - Booking outbound to ACS: `Acs:InternalToken`
  - Booking outbound to ACS function auth: `Acs:FunctionKey`
  - Booking outbound to calendar: `Calendars:InternalToken`
  - Booking outbound to calendar function auth: `Calendars:FunctionKey`
  - Booking outbound to location: `LocationService:InternalToken`
  - Booking outbound to location function auth: `LocationService:FunctionKey`
  - Booking adviser projection sync: `AdviserDirectory:InternalToken`
  - Location inbound: `InternalApiAuth:*`
  - Location outbound to calendar: `CalendarService:InternalToken`
  - Location outbound to calendar function auth: `CalendarService:FunctionKey`
  - Calendar inbound: `InternalApiAuth:*`
  - Calendar outbound to booking webhook function auth: `GraphWebhook:BookingFunctionKey`
  - Calendar outbound to booking webhook: `GraphWebhook:BookingInternalToken`
- Internal function keys remain part of the design for function apps, but master-key and `?code=` query-string usage should not be used on routine service-to-service paths.

## Domain User Auth Model
- Domain-user authentication is separate from the internal service-to-service bearer token model.
- The Vue app signs users in with Microsoft Entra ID / Azure AD and sends the Entra access token to Booking as `Authorization: Bearer <token>`.
- Booking validates issuer, audience, lifetime, tenant, and optional domain restrictions before treating the request as authenticated.
- Booking resolves application roles and derived capabilities server-side from claims and `DomainUserAuth:*` role-mapping configuration.
- `GET /api/v1/me` is the frontend bootstrap endpoint for current user identity, roles, and capabilities.
- Frontends may use the returned roles/capabilities for menus and route guards, but Booking remains the authority for policy enforcement.

## Public And Webhook Exceptions
- Calendar notifications must remain externally callable because Microsoft Graph performs the callback challenge and delivery.
- Health and docs remain public to support probes and documentation discovery.
- Development-only auth relaxation is opt-in through `InternalApiAuth:AllowAnonymousInDevelopment=true`.

## Configuration Model
- Checked-in backend config now uses `local.settings.template.json` examples instead of active local settings.
- Critical service URLs, internal tokens, and timezone settings are read from typed options rather than hard-coded values.
- Lifecycle reason-code source, lifecycle notification behavior, escalation placeholders, and governance placeholders are also configuration-backed through `Lifecycle:*`.
- Do not commit real secrets or live URLs into backend settings files.

## Cached Search And Projection Model
- Booking remains orchestration-only on the hot path:
  - it asks Location for ranked adviser candidates
  - it asks Calendar for schedule/conflict validation, hold updates, confirm updates, and cancel updates
- Location hot path now prefers:
  - `AdviserReferenceCache` for adviser profile/filtering inputs
  - `GeoCacheEntries` for coordinate lookups
  - `RouteCacheEntries` for routing/travel reuse
  - Calendar batch schedule reads with `PreferCached`
- Calendar owns the Graph anti-corruption layer and its local operational read model:
  - `CalendarProjectionEvents`
  - `MailboxProjectionStates`
- Freshness policy:
  1. search and ranking use cached/batched reads where possible
  2. booking confirmation/conflict validation uses fresher calendar reads before mutation
  3. live calendar reads write back into the calendar projection

## Timezone Strategy
- UTC remains the storage and transport baseline for booking, location, and calendar flows.
- Booking default timezone is read from `Calendar:DefaultTimezone`.
- Location business-hour evaluation is read from `BusinessTime:TimeZone`.
- Hard-coded `Europe/London` usage was reduced to configuration-backed defaults where the services actually make scheduling decisions.

## Provider Selection Rules
- Location v1 continues to use Azure Maps-backed services.
- Google geocoding/routing paths are intentionally disabled because they are incomplete.
- `Maps:Google:Enabled=true` now fails fast at startup, and direct Google provider calls throw instead of returning fake `(0,0)` or zero-distance route data.

## Booking Hold And Confirm Semantics
- Confirm responses now return machine-readable error codes for:
  - `HoldCancelled`
  - `HoldExpired`
  - `HoldAlreadyConfirmed`
  - `HoldSlotMissing`
  - `HoldTransactionMissing`
  - `HoldStateInvalid`
- This keeps downstream handling more consistent without changing the outer API envelope shape.

## Lifecycle Audit Model
- Booking SQL is the lifecycle source of truth for cancellation and rearrangement in this phase.
- New SQL-backed records:
  - `LifecycleEvents`: event-level audit rows with booking ID, transaction ID, actor metadata, reason metadata, before/after payloads, correlation ID, and timestamp.
  - `LifecycleSteps`: ordered step history for each lifecycle event.
  - `NotificationDispatches`: extended to link dispatches back to lifecycle events and persist outcome/failure details.
- The lifecycle payload shape supports:
  - transaction ID
  - actor type and actor ID
  - reason code and notes
  - before/after JSON payloads
  - lifecycle event type
  - correlation ID
  - step status and error details

## Outlook Governance Model
- Layer placement stays explicit:
  - `Functions`: trigger/auth/request mapping only.
  - `Application`: conflict checks and calendar-governance workflows.
  - `Domain`: lifecycle constants and typed options.
  - `Infrastructure`: SQL repositories, notification persistence, and calendar adapter parsing.
- Booking confirmation now performs an application-layer conflict check before any Outlook mutation.
- Conflict findings are written to SQL-backed `OperationalIssues` with structured metadata and machine-readable codes such as:
  - `BookingConflictDoubleBooked`
  - `BookingConflictBufferViolation`
- Calendar notification processing now evaluates snapshots for:
  - incorrect `ShowAs`
  - missing location on in-person bookings
  - recurrence anomalies
  - event-window tampering
  - deletion attempts
- Adviser notifications are recorded first with client-sensitive details omitted.
- Repeated issues escalate to configurable manager recipients based on `OutlookGovernance:*` threshold/window settings.
- Deletion/tampering currently uses the safest controlled reconciliation path available in this repo:
  - log `DeletionAttemptDetected`
  - mark the issue as `ReconciliationRequired`
  - notify adviser/escalation targets
  - defer true event recreation governance to a later phase

## Sequencing Model
- Cancellation and rearrangement now flow through dedicated orchestrators in the booking application layer:
  - `CancellationOrchestrator`
  - `RearrangementOrchestrator`
  - `LifecycleAuditService`
- The intended sequence is explicit in service code:
  1. Outlook/calendar action first
  2. SQL lifecycle and audit persistence second
  3. Notifications third
- Location and calendar remain integration boundaries in this phase. Booking owns the audit timeline because it already owns booking state and SQL persistence.

## Deferred Items
- Adviser approval remains at the existing function/approval-service boundary; it is not yet moved into the lifecycle orchestrators.
- Full Outlook auto-restore and deletion-governance enforcement remain partial; current code records and escalates controlled reconciliation rather than recreating deleted events automatically.
- ACS no longer exposes the pure Graph calendar/user lookup or SharePoint-backed adviser lookup endpoints on the active repo surface.
- Two legacy ACS SharePoint-backed endpoints remain temporarily for ATR/client-overview and transcription reads; they are explicitly deprecated and are outside the booking lifecycle path.
- Booking verification is still partially blocked by compile issues in `tests/AFH.Booking.Tests` after infrastructure cleanup; that is now the main booking-side verification gap rather than the retired duplicate approval workflow file.
- No dashboard or UI work is included in this phase.
- EF migrations for the new lifecycle tables/columns, including `OperationalIssues`, should be created before deployment to a shared environment.

## Local Development Notes
- Copy each service's `local.settings.template.json` to `local.settings.json`.
- Use the same shared internal token across the three backend services when running them together locally.
- If local auth relaxation is enabled, keep it in development settings only and never promote it to shared or production config.
