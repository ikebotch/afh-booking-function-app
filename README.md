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
- Notification bounded context owns execution (templates, channels, routing).
- Email is the first channel supported; SMS and Push are future channels.
- Templates are currently `.txt` only; no HTML/multipart yet.
- Bouncebacks are provider feedback handled explicitly by Notification Infrastructure.
- Contact-centre copy is a routing policy evaluated by the Notification service, not hardcoded template logic.
- Hold notifications are enabled; they should be configuration-gated before production if the business has not explicitly approved them.
- Old direct Booking email paths are still intentionally present for transition. Do not remove them until tests prove safe replacement.
- **Wording Note:** Current live lifecycle wording uses `Rearranged`, whereas notification template naming uses `Rescheduled`. Do not change wording in Sprint 7 unless product confirms it.

## SQL Migration Note
- Lifecycle and Outlook-governance changes now require database schema support for lifecycle audit tables and `OperationalIssues`.
- Create and apply an EF migration from the infrastructure project before deploying to shared environments.
- Treat that migration as required infra work for this backend phase.

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
