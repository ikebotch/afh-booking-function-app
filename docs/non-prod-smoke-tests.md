# Non-Prod Smoke Tests

## Purpose
This checklist is for realistic non-production validation of the active booking path without changing ownership boundaries.

## Required Configuration
- Booking DB connection string
- Calendar service base URL, function key, and shared internal bearer token
- Location service base URL, function key, and shared internal bearer token
- ACS service base URL, function key, and shared internal bearer token
- Notification provider settings
- XPlan base URL and API key if downstream publishing is enabled

## Core Journeys
1. Create an in-person booking and confirm:
   - Booking persists the hold/confirmation
   - Calendar event is created
   - notification dispatch record is written
2. Create a remote booking:
   - Booking creates/updates the booking lifecycle
   - ACS meeting link/session data is available
   - Calendar event still remains owned by Calendar
3. Cancel a booking:
   - Outlook/calendar action happens first
   - lifecycle SQL/audit entries are written second
   - notifications run third
4. Rearrange a booking:
   - new booking is confirmed
   - old booking is cancelled
   - lifecycle audit links old/new booking ids
5. Trigger calendar notification processing:
   - Booking records intake
   - governance/operational issues appear if event hygiene is broken
6. Verify downstream publish and reconcile:
   - a `DownstreamUpdates` row is written
   - if XPlan is intentionally unavailable, row moves to `Failed`
   - `POST /api/v1/admin/downstream-updates/reconcile` retries the row once the dependency is restored

## Notes
- The reconciliation endpoint is intentionally explicit. It is meant for controlled retry of stale downstream rows rather than silent automatic replay.
- Use `x-correlation-id` on all manual requests so Booking, Calendar, Location, and ACS logs can be traced together.
- Do not use master keys for routine service-to-service smoke tests; use the target function key plus `Authorization: Bearer <internal token>`.
