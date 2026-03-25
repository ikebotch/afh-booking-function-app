# AFH Booking Service

## What It Owns
- Booking orchestration and booking operational state.
- Hold, confirm, cancel, rearrange, approval, and projection workflows.
- Internal calls to the location service for in-person adviser search.
- Internal calls to the calendar service for appointment and subscription work.

## Local Setup
1. Copy `src/AFH.Booking.Functions/local.settings.template.json` to `src/AFH.Booking.Functions/local.settings.json`.
2. Fill in connection strings, external integrations, function keys, and the shared internal bearer token.
3. Keep `InternalApiAuth:AllowAnonymousInDevelopment=true` only for local development.

## Internal Auth
- Booking-to-calendar, booking-to-location, and booking-to-ACS internal calls now use function-specific keys plus `Authorization: Bearer <token>`.
- Booking internal calendar routes also require function-level auth plus the shared bearer token via `InternalApiAuth:*`.
- Do not use the master key for routine backend-to-backend traffic.

## Build And Test
- `dotnet test AFH.BookingService.sln`

## SQL Migration Note
- Lifecycle and Outlook-governance changes now require database schema support for lifecycle audit tables and `OperationalIssues`.
- Create and apply an EF migration from the infrastructure project before deploying to shared environments.
- Treat that migration as required infra work for this backend phase.

## More Detail
- See `docs/backend-architecture.md` for service boundaries, endpoint classification, configuration, and timezone handling.
- The same document now also covers lifecycle SQL/audit tables, orchestration sequencing, and deferred cancellation/rearrangement work for later phases.
