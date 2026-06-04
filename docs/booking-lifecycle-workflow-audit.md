# Booking Lifecycle Workflow Architecture Audit

Date: 2026-06-04

Scope:
- Function endpoints under `src/AFH.Booking.Function/Functions/V1/Bookings`
- Booking application services under `src/AFH.Booking.Application/Services/Bookings`
- Hold services under `src/AFH.Booking.Application/Services/Bookings/Holds`
- Lifecycle and notification services under `src/AFH.Booking.Application/Services/Lifecycle` and `src/AFH.Booking.Application/Services/Notifications`
- Calendar infrastructure boundary under `src/AFH.Booking.Infrastructure/Calendar`
- Relevant regression tests under `tests/AFH.Booking.Tests`

This is an audit-only report. It documents the current architecture and a staged implementation plan; it does not perform the refactor.

## Executive Summary

The booking system is already partly aligned with the target principle. Most endpoint functions authenticate, validate route/body shape, build commands, and delegate into application services. Cancellation and final rearrangement are the strongest areas: self-service, LeadTech, admin/internal, and adviser-approval execution paths all converge on shared application orchestrators.

The main gaps are not isolated business engines in every caller. The larger risks are:

- Actor/source context is modelled as loose strings on commands rather than a standard workflow context.
- Hold creation, hold release, and hold expiry are not fully aligned with the lifecycle/audit/notification model.
- Lifecycle event and step writing is duplicated across workflows, with different defaults and step sets.
- Notification dispatch is mostly workflow-owned, but hold-created notification is emitted without a corresponding lifecycle event, and no-show has no notification policy decision recorded.
- Idempotency and duplicate-submit protections exist in spots, but they are not expressed as a common workflow concern.
- Calendar side effects are application-owned, which is correct, but step outcomes are not recorded consistently for hold create/release/expiry.

The recommended migration is incremental: introduce `BookingActorContext`, add a small workflow outcome/audit helper, centralise hold lifecycle side effects, then tighten cancellation/rearrangement around the shared context without changing external contracts.

## Current Endpoint Map

| Function | Route / trigger | Actor path | Current application service | Assessment |
| --- | --- | --- | --- | --- |
| `Bookings_CreateHold` | `POST /v1/bookings/hold` | Internal/service route | `ICreateBookingService.HandleAsync` | Thin endpoint. Service owns slot/context load, hold creation/replacement, calendar tentative event, hold notification. Missing lifecycle event for hold created. |
| `Bookings_ConfirmHold` | `POST /v1/bookings/holds/{holdId}/confirm` | Internal/service route | `IConfirmBookingService.HandleAsync` | Thin endpoint. Service owns final route-time check, calendar busy update, booking state, lifecycle audit, notification. Actor is hard-coded to `Client`. |
| `Bookings_ReleaseHold` | `POST /v1/bookings/holds/{holdId}/release` | Internal/service route | `IReleaseHoldService.HandleAsync` | Thin endpoint. Service owns release and calendar cancel. Missing lifecycle event/steps; calendar failures are swallowed. |
| `Holds_Cleanup` | Timer every 2 minutes | System job | `IBookingHoldRepository.GetExpiredActiveAsync` then `IReleaseHoldService.HandleAsync` | Uses shared release service, which is good. However expiry is indistinguishable from manual release because the service has no actor/reason command. |
| `Bookings_CancelBooking` | `POST /v1/bookings/{bookingId}/cancel` | Internal/admin-style function route | `ICancelBookingService.HandleAsync` | Thin endpoint. Cancellation delegates to shared service/orchestrator. Defaults `RequestedBy` to `Client`, which is risky for an internal route. |
| `Bookings_SelfServiceCancel` | `POST /v1/self-service/bookings/{bookingId}/cancel?token=...` | Self-service client | `IBookingChangeAccessService` then `ICancelBookingService.HandleAsync` | Correctly validates token against route booking id before shared cancellation. Public-safe problem mapping is present, but service messages still surface directly. |
| `Bookings_LeadTechCancel` | `POST /v1/leadtech/bookings/{bookingId}/cancel` | LeadTech | `ICancelBookingService.HandleAsync` | Thin endpoint. Access policy requires authenticated user and `CancelAsLeadTech`; business logic is shared. |
| `Bookings_GetRearrangementOptions` | `POST /v1/bookings/{bookingId}/rearrangement/options` | Internal/admin-style function route | `IRearrangementOptionsService.HandleAsync` | Thin endpoint. Options are shared. Caller context is not explicit. |
| `Bookings_SelfServiceRearrangementOptions` | `POST /v1/self-service/bookings/{bookingId}/rearrangement/options?token=...` | Self-service client | `IBookingChangeAccessService` then `IRearrangementOptionsService.HandleAsync` | Correctly validates token first. Service uses original booking transaction ref for in-person client lookup and availability option transactions for slots. |
| `Bookings_LeadTechRearrangementOptions` | `POST /v1/leadtech/bookings/{bookingId}/rearrangement/options` | LeadTech | `IRearrangementOptionsService.HandleAsync` | Thin endpoint. Access policy requires authenticated user and `RearrangementOptionsRead`. |
| `Bookings_Rearrange` | `POST /v1/bookings/{bookingId}/rearrange` | Internal/admin-style function route | `IRearrangeBookingService.HandleAsync` | Thin endpoint. Final rearrangement uses shared service/orchestrator. Defaults `RequestedBy` to `Client`, which is risky for an internal route. |
| `Bookings_SelfServiceRearrange` | `POST /v1/self-service/bookings/{bookingId}/rearrange?token=...` | Self-service client | `IBookingChangeAccessService` then `IRearrangeBookingService.HandleAsync` | Correct route/current booking id contract. Body requires only `newSlotId`; option transaction resolution is backend-owned. |
| `Bookings_LeadTechRearrange` | `POST /v1/leadtech/bookings/{bookingId}/rearrange` | LeadTech | `IRearrangeBookingService.HandleAsync` | Thin endpoint. Access policy requires authenticated user and `RearrangeAsLeadTech`; business logic is shared. |
| `Bookings_RecordNoShow` | `POST /v1/bookings/{bookingId}/no-show` | Internal/service route | `INoShowBookingService.HandleAsync` | Thin endpoint. Service validates confirmed state and writes lifecycle audit. No notification/calendar policy step. |
| `Bookings_RemediateShowAs` | `POST /v1/bookings/{bookingId}/calendar/remediate-showas` | Internal/admin | `IBookingShowAsRemediationService.HandleAsync` | Technical remediation, not a booking lifecycle transition. Calendar update is isolated in an application service. |
| `Bookings_GetBooking` | `GET /v1/bookings/{bookingId}` | Internal/admin | `IBookingDetailsService.GetAsync` | Read-only; not lifecycle-changing. |
| `Bookings_SelfServiceGetBooking` | `GET /v1/self-service/bookings/{bookingId}?token=...` | Self-service client | `IBookingChangeAccessService` then `IBookingDetailsService.GetAsync` | Read-only; correctly validates token first. |
| `Bookings_CreateApprovalRequest` | `POST /v1/bookings/{bookingId}/approval-requests` | Adviser/internal approval path | `IApprovalWorkflowService.CreateAsync` | Adjacent governance workflow. It writes approval lifecycle events and later executes shared cancellation/rearrangement orchestrators. |
| `Approvals_ListPending` | `GET /v1/approval-requests/pending` | Admin/manager | `IApprovalWorkflowService.ListPendingAsync` | Read-only approval workflow. |
| `Approvals_Review` | `POST /v1/approval-requests/{requestId}/review` | Admin/manager | `IApprovalWorkflowService.ReviewAsync` | Approval decision path. Approved cancellation/rearrangement execution delegates to shared orchestrators. |
| `Bookings_SendNotification` | `POST /v1/bookings/{bookingId}/notifications/send` | Internal/admin notification | `IBookingNotificationRequestService.SendAsync` | Manual notification path. Should remain separate from lifecycle workflow policy except for explicit admin resend/use cases. |
| `Bookings_RecordEmailBounce` | `POST /v1/notifications/email/bounces` | Internal notification callback | `IEmailBounceService.RecordAsync` | Notification delivery/audit concern, not booking lifecycle policy. |

## Workflow and Side-Effect Matrix

| Workflow | Current shared service | State changes | Calendar side effects | Lifecycle audit | Notification behaviour | Downstream/event behaviour |
| --- | --- | --- | --- | --- | --- | --- |
| Hold creation | `CreateBookingService`, `BookingHoldService`, `BookingCalendarService` | Creates or reopens a hold; supersedes other active holds on same transaction; extends transaction expiry | Creates tentative Outlook event after conflict check | No lifecycle event for `HoldCreated`; no lifecycle steps | Calls `IBookingNotificationStep` for `HoldCreated`, but outside a lifecycle event/step record | None found |
| Hold confirmation / booking creation | `ConfirmBookingService` | Confirms hold; marks open transaction completed | Final route-time guard for in-person; creates remote join link; updates existing hold event to busy/confirmed | Records `Booked` event with Outlook, SQL audit, notification steps | Executes `BookingConfirmed` through `IBookingNotificationStep` | None found |
| Manual hold release | `ReleaseHoldService` | Cancels active hold with reason `Released by user` | Cancels provider event when present | None | None | None |
| Hold expiry cleanup | `HoldsCleanupFunction` plus `ReleaseHoldService` | Same as manual release | Same as manual release | None; expiry is not distinguished from user release | None | Timer logs only |
| Cancellation | `CancelBookingService`, `CancellationOrchestrator` | Cancels confirmed/actionable hold | Cancels provider event when present | Records `Cancelled` event with Outlook, SQL audit, notification steps | Executes `BookingCancelled` when `sendClientNotification` is true | Publishes booking change through `IDownstreamUpdateService` |
| Rearrangement options | `RearrangementOptionsService`, `IAvailabilityService` | Creates availability option transactions/slots; does not hold | Availability/calendar view used by availability pipeline | None | None | None |
| Final rearrangement | `RearrangeBookingService`, `RearrangementOrchestrator` | Resolves option slot, creates and confirms replacement booking, cancels previous booking | Uses create/confirm/cancel workflows; old booking cancellation notification suppressed | Records replacement `Rearranged` event with SQL audit and notification steps; old booking cancellation also records `Cancelled` via cancellation orchestrator | Executes `BookingRescheduled`; suppresses separate client cancellation notification for old booking | Publishes booking change through `IDownstreamUpdateService` |
| No-show | `NoShowBookingService` | Does not mutate hold status; records lifecycle transition only | None | Records `No Show` event and SQL audit step | None | None |
| Calendar show-as remediation | `BookingShowAsRemediationService` | No booking state change | Updates provider event to busy with remediation categories | None | None | None |
| Adviser approval request/review | `ApprovalWorkflowService` | Approval rows/history; approved execution calls cancellation/rearrangement orchestrators | Via called workflow when approved | Records approval requested/decision/execution events using same audit store | Uses approval notification service, not booking lifecycle notification step | Approved execution delegates to shared lifecycle workflow |

## Answers to Audit Questions

1. Which endpoints currently implement booking business logic directly?
   - None of the core HTTP endpoints perform full booking lifecycle logic directly.
   - `Holds_Cleanup` directly queries expired holds before delegating release. That is acceptable scheduler logic, but it lacks an expiry-specific workflow command/context.
   - Endpoint functions still contain caller-specific defaults such as `RequestedBy = Client` on internal cancel/rearrange routes. That is not full business logic, but it is business metadata leakage.

2. Which endpoints already delegate properly to shared services?
   - Create hold, confirm hold, release hold, cancellation, rearrangement options, final rearrangement, no-show, booking details, approval review, and show-as remediation all delegate to application services.
   - Self-service functions correctly validate token access before invoking shared services.
   - LeadTech functions delegate into the same cancellation/rearrangement services as other actors.

3. Are self-service, LeadTech and admin paths using the same workflow for cancellation?
   - Yes for active cancellation: all call `ICancelBookingService.HandleAsync`, which delegates to `ICancellationOrchestrator.CancelAsync`.
   - Adviser approval execution calls `ICancellationOrchestrator.CancelAsync` directly after approval.
   - Gap: route-specific actor/source context is passed as ad-hoc command fields, and internal function-route defaults can mislabel actor type.

4. Are self-service, LeadTech and admin paths using the same workflow for rearrangement?
   - Yes for final submit: all call `IRearrangeBookingService.HandleAsync`, which delegates to `IRearrangementOrchestrator.RearrangeAsync`.
   - Adviser approval execution calls `IRearrangementOrchestrator.RearrangeAsync` directly after approval.
   - Rearrangement options are shared through `IRearrangementOptionsService`.

5. Are hold creation and hold confirmation aligned with the same lifecycle/audit/notification model?
   - Confirmation is aligned with lifecycle/audit/notification.
   - Hold creation is only partially aligned: it creates/replaces holds and emits hold-created notification, but there is no `HoldCreated` lifecycle audit event and no recorded notification step attached to such an event.
   - Hold release/expiry are not aligned with lifecycle/audit/notification.

6. Are lifecycle events emitted consistently for confirmed, cancelled, rescheduled, hold released, hold expired and no-show?
   - Confirmed: yes, `Booked`.
   - Cancelled: yes, `Cancelled`.
   - Rescheduled: yes, `Rearranged`, plus cancellation event for the previous booking.
   - No-show: yes, `No Show`.
   - Hold released: no.
   - Hold expired: no.
   - Hold created: notification type exists, but lifecycle state machine does not currently map `HoldCreated` to a lifecycle state.

7. Are audit records written consistently across actors?
   - Cancellation and rearrangement preserve actor type/id from commands.
   - No-show preserves actor type/id after validating a limited actor list.
   - Confirmation hard-codes actor type to `Client`.
   - Hold creation/release/expiry do not write lifecycle audit records.
   - Approval events write audit records but use approval-specific event types outside `BookingLifecycleStateMachine.ResolveStateForEventType`; they rely on same previous/new state.

8. Are notifications triggered consistently from workflow outcomes rather than individual endpoints?
   - Yes for confirmed, cancelled, and rearranged bookings.
   - Hold-created notification is workflow-owned, but not tied to a lifecycle event/step.
   - No-show has no notification step, so the policy decision is implicit rather than recorded.
   - Manual notification send remains a separate admin workflow, which is appropriate.

9. Is actor/source context preserved consistently?
   - Partially.
   - Current commands carry `RequestedBy`, `ActorId`, `CorrelationId`, reason fields, and approval id.
   - There is no standard `BookingActorContext` containing `SourceApplication`, `ActorType`, `DisplayName`, `IsSelfService`, `CanOverrideRules`, or capability flags.
   - As a result, source system is hard-coded as `BookingService` in multiple services, and public/internal routes can accidentally use different defaults.

10. Are idempotency and duplicate-submit protections consistent?
    - Partial.
    - Hold confirmation and release use `GetForUpdateAsync`; hold and transaction models have row versions.
    - Hold creation has create-or-replace semantics and a unique slot hold constraint.
    - Cancellation is idempotent for non-client callers when already cancelled, but self-service repeat cancellation returns conflict.
    - Rearrangement has strong slot-option validation and stops before cancelling the old booking if replacement confirmation fails, but there is no explicit idempotency key/result cache for duplicate final submits.
    - Notification outbox has idempotency support, but lifecycle workflows do not consistently provide explicit idempotency keys.

11. Are calendar side effects handled consistently?
    - Calendar provider integration is correctly hidden behind `ICalendarGateway`; infrastructure performs technical calls only.
    - Application services decide when to create/update/cancel events, which matches the target principle.
    - Step outcomes are inconsistent: cancellation, rearrangement, and confirmation record Outlook steps; hold creation/release/expiry do not.
    - `ReleaseHoldService` swallows calendar cancellation exceptions without audit visibility.

12. Are public self-service errors safely shaped compared with internal/admin errors?
    - Self-service endpoints use token validation and problem responses.
    - However, they surface application service error messages directly. Some messages include internal ids such as slot ids and transaction context details.
    - A public-safe error mapper should translate workflow errors for self-service routes while preserving detailed logs/audit internally.

13. Are transaction ids, transaction refs, booking ids, slot ids and availability transaction ids used consistently?
    - The current rearrangement contract is correct: route `bookingId` is the existing booking id; request body contains only `newSlotId`; selected slot option transaction is resolved internally.
    - In-person rearrangement options use original booking `TransactionRef` for client/prospect lookup and option transactions only for availability/slot lookup.
    - The naming in `GetAvailabilityQuery` is still overloaded: `TransactionId` can be used as an availability transaction context while `ClientLookupRef` is needed for leads lookup. This should be documented in types or replaced with clearer value objects.

14. Are there places where LeadTech/self-service/admin use different validation logic for the same business action?
    - Business validation for cancellation and final rearrangement is shared.
    - Caller access validation differs by design: self-service token, LeadTech permission, internal function auth.
    - The remaining differences are metadata/defaults, not core rules: `RequestedBy` defaults, actor ids, and public error shaping.
    - Adviser approval adds pre-approval validation before executing shared cancellation/rearrangement.

15. Are there any places where Notification or infrastructure owns Booking policy decisions incorrectly?
    - Infrastructure calendar code is technical and does not appear to own booking policy.
    - Notification policy owns channel/recipient enablement, which is appropriate.
    - Booking services still build notification data/templates directly. That is application policy, but it is duplicated across workflows and should move behind a workflow notification adapter.
    - Hold-created notification currently bypasses lifecycle audit, which makes notification policy look detached from booking lifecycle policy.

## Gap List

1. No standard `BookingActorContext`.
   - Current actor/source fields are spread across commands and hard-coded constants.
   - Future callers such as contact centre or manager users will likely add more branches unless a common context is introduced.

2. Hold lifecycle is incomplete.
   - `HoldCreated` notification exists, but lifecycle audit does not record hold creation.
   - Manual release and timer expiry do not record `HoldReleased` or `HoldExpired`.
   - Cleanup cannot distinguish system expiry from user release because `ReleaseHoldService` accepts only `holdId`.

3. Lifecycle state machine excludes hold states.
   - Current valid states are `Booked`, `Rearranged`, `Cancelled`, and `No Show`.
   - This prevents first-class hold created/released/expired transitions unless holds are modelled as events without lifecycle state, or the state machine is expanded.

4. Lifecycle/audit/notification step recording is duplicated.
   - Confirmation, cancellation, rearrangement, and no-show each create event/step records manually.
   - Step sequencing is not expressed through a shared workflow outcome model.

5. Calendar failure handling is uneven.
   - Cancellation records failed Outlook cancellation and continues.
   - Release swallows provider cancellation failures silently.
   - Hold creation fails the operation on calendar create/conflict failure but records no lifecycle step.

6. Public error shaping is not isolated.
   - Self-service problem responses are built from service failures.
   - The same detailed messages are used by internal and public routes.

7. Idempotency is local, not a workflow contract.
   - Existing row locks and unique constraints help, but duplicate submits are not handled consistently across create/confirm/cancel/rearrange.
   - Rearrangement is particularly sensitive because it creates a replacement booking then cancels the old booking.

8. Approval events are adjacent to lifecycle, but not fully typed.
   - Approval events use strings such as `ApprovalRequested` and same-state transitions.
   - That may be fine, but should be formalised as governance events or separated from booking lifecycle transitions.

9. Notification data construction is duplicated in workflow services.
   - Confirmation, cancellation, rearrangement, and hold creation each build recipient/data payloads locally.
   - A workflow notification adapter would reduce drift.

10. Show-as remediation is rightly separate but could be mistaken for lifecycle.
    - It should remain a technical calendar remediation workflow with its own audit/observability if needed, not a booking state transition.

## Recommended Target Design

### BookingActorContext

Add a standard context object and pass it into lifecycle-changing workflow commands.

Suggested shape:

```csharp
public sealed record BookingActorContext(
    string SourceApplication,
    string ActorType,
    string? ActorId,
    string? DisplayName,
    string? CorrelationId,
    bool IsSelfService,
    bool CanOverrideRules,
    IReadOnlySet<string> Permissions);
```

Endpoint/function layer should own construction:

- Self-service client:
  - validates token against route booking id
  - `SourceApplication = "SelfService"`
  - `ActorType = LifecycleActors.Client`
  - `ActorId` from token envelope
  - `IsSelfService = true`
  - no override capability
- LeadTech:
  - validates authenticated user and permission
  - `SourceApplication = "LeadTech"`
  - `ActorType = LifecycleActors.LeadTech`
  - actor id/display name from auth principal when available
- Admin / booking agent:
  - validates authenticated user and permission
  - `SourceApplication = "ControlCentre"` or `"Admin"`
  - `ActorType = "BookingAgent"` or another typed constant
  - permissions/capabilities from RBAC
- Adviser approval execution:
  - `SourceApplication = "ApprovalWorkflow"`
  - `ActorType = LifecycleActors.Adviser`
  - `ActorId` requester id
  - correlation id from approval request/review
- System jobs:
  - `SourceApplication = "BookingJobs"`
  - `ActorType = LifecycleActors.System`
  - `ActorId = "HoldsCleanup"` or job name

### Workflow Boundaries

Application workflows should own:

- load booking/current state
- validate business rules
- validate availability/slot context
- create/update/cancel calendar events through `ICalendarGateway`
- mutate booking/hold/transaction state
- record lifecycle event and ordered steps
- trigger notifications according to workflow outcome
- publish downstream changes
- handle idempotency/concurrency
- return an application result with both internal diagnostics and public-safe error code

Function endpoints should own:

- route/query/body parsing
- token validation
- function/internal/authenticated user checks
- permission checks
- `BookingActorContext` creation
- mapping caller DTOs into workflow commands
- mapping workflow responses to public/internal DTOs

Infrastructure should own only technical integration:

- EF persistence and row-version handling
- calendar provider HTTP calls
- notification publishing/outbox transport
- queues
- external client adapters

### Suggested Workflow Services

Keep existing service names where possible and standardise their contracts:

- `BookingCreationWorkflow` or evolve `CreateBookingService`/`ConfirmBookingService`
- `BookingHoldWorkflow` for create/reopen/release/expire, or split into:
  - `BookingHoldCreationWorkflow`
  - `BookingHoldReleaseWorkflow`
- `BookingCancellationWorkflow` or evolve `CancellationOrchestrator`
- `BookingRearrangementWorkflow` or evolve `RearrangementOrchestrator`
- `BookingNoShowWorkflow` or evolve `NoShowBookingService`
- `BookingLifecycleRecorder` helper for event + ordered steps
- `BookingWorkflowNotificationAdapter` for recipient/data building and notification step execution

### Lifecycle Event Model

Choose one of these before implementation:

Option A: expand lifecycle states:

- `HoldCreated`
- `HoldReleased`
- `HoldExpired`
- `Booked`
- `Rearranged`
- `Cancelled`
- `NoShow`

Option B: keep booking lifecycle states only for confirmed bookings, but add auditable hold events:

- Hold events have `PreviousState`/`NewState = null` or a separate hold state column/category.
- Booking lifecycle state machine continues to model only confirmed booking states.

Recommendation: Option B is safer if reports currently assume lifecycle state starts at `Booked`. Add event types `HoldCreated`, `HoldReleased`, and `HoldExpired`, but do not force them into confirmed-booking state transitions until reporting requirements are agreed.

## Step-by-Step Implementation Plan

### Commit 1: `docs(booking): audit lifecycle workflow architecture`

- Add this audit report.
- No code changes.
- Run targeted test filter and `git diff --check`.

### Commit 2: `refactor(booking): introduce booking actor workflow context`

- Add `BookingActorContext` to application models.
- Add non-breaking overloads or optional properties to `CreateHoldCommand`, `ConfirmBookingCommand`, `CancelBookingCommand`, `RearrangeBookingCommand`, `GetRearrangementOptionsCommand`, `RecordNoShowCommand`, and release/expiry commands.
- Add endpoint mapping helpers:
  - self-service token to actor context
  - LeadTech/user-auth to actor context
  - internal/system to actor context
- Keep old command fields temporarily and derive them from context for compatibility.
- Add tests that context flows to lifecycle actor/source/correlation fields.

### Commit 3: `refactor(booking): standardise lifecycle event recording`

- Introduce `IBookingLifecycleRecorder`.
- Centralise:
  - event creation
  - source system from `BookingActorContext.SourceApplication`
  - actor type/id
  - ordered step creation
  - correlation id propagation
- Move confirmation, cancellation, rearrangement, and no-show to the recorder without changing behaviour.
- Preserve existing step names and sequencing.
- Keep `LifecycleOrchestratorSequencingTests` green.

### Commit 4: `refactor(booking): align hold creation lifecycle workflow`

- Decide Option A or B for hold events.
- Record hold-created audit event when a hold is created/reopened.
- Record calendar tentative event result as an Outlook step.
- Record hold-created notification as a notification step tied to that event.
- Include superseded-hold handling in the audit trail, either as separate events or as details on the new hold event.
- Tests:
  - hold creation records hold-created audit event
  - hold calendar conflict/failure is reflected in outcome
  - hold notification failure does not roll back successful hold creation if that remains desired policy

### Commit 5: `refactor(booking): centralise hold release and expiry workflow`

- Replace `ReleaseHoldService.HandleAsync(string holdId)` with a command-based workflow, keeping the old method as a compatibility wrapper during migration.
- Add command fields:
  - `HoldId`
  - `ReasonCode`
  - `ReasonDetail`
  - `BookingActorContext`
  - `ReleaseKind = ManualRelease | Expiry`
- Have `HoldsCleanupFunction` call expiry command with system actor context.
- Record hold released/expired audit event and Outlook step outcome.
- Stop swallowing calendar exceptions silently; convert them to recorded failed step while preserving current release policy if required.
- Tests:
  - manual release records `HoldReleased`
  - cleanup records `HoldExpired`
  - confirmed holds still cannot be released
  - expired/cancelled release remains idempotent

### Commit 6: `refactor(booking): centralise cancellation workflow context`

- Evolve `CancellationOrchestrator` to accept actor context.
- Remove hard-coded `SourceSystem = "BookingService"` and route default `RequestedBy = Client` for internal function route.
- Add explicit admin/internal actor mapping.
- Keep adviser approval requirement in `CancelBookingService` until approval workflow is formalised.
- Tests:
  - self-service, LeadTech, admin, adviser approval all produce expected actor/source
  - already-cancelled behaviour remains unchanged by actor type
  - calendar failure still records failed Outlook step

### Commit 7: `refactor(booking): centralise rearrangement workflow context`

- Evolve `RearrangementOptionsService` and `RearrangementOrchestrator` to accept actor context.
- Preserve current external contract:
  - route booking id is current booking id
  - final body contains only `newSlotId`
  - backend resolves option transaction internally
  - no self-service hold endpoint
- Keep original booking transaction ref/client lookup ref distinct from availability option transaction id.
- Tests:
  - self-service/LeadTech/admin/adviser approval actor/source recorded consistently
  - assigned and alternative option transactions resolve internally
  - in-person client lookup uses original booking transaction ref
  - wrong booking token cannot use another booking's option

### Commit 8: `refactor(booking): standardise workflow notification adapter`

- Add `IBookingWorkflowNotificationAdapter` to build recipients and notification data for:
  - hold created
  - booking confirmed
  - booking cancelled
  - booking rearranged
  - optional no-show if policy requires
- Keep `BookingNotificationStep` as the generic policy/publisher bridge.
- Add idempotency keys to workflow notification data consistently.
- Tests:
  - expected notification types map from lifecycle events
  - duplicate notification submit uses outbox idempotency
  - no-recipient/policy-disabled outcomes are recorded as skipped steps

### Commit 9: `refactor(booking): add workflow idempotency guard`

- Define an application-level idempotency strategy for lifecycle commands.
- Prefer caller-provided idempotency key when available; otherwise derive safe keys:
  - confirmation: `confirm:{holdId}`
  - cancellation: `cancel:{bookingId}:{actorType}:{reasonCode}`
  - rearrangement: `rearrange:{oldBookingId}:{newSlotId}:{actorType}`
  - release/expiry: `release:{holdId}:{releaseKind}`
- Use an `IIdempotencyGuard`/store implementation around side-effecting workflow entry points.
- For rearrangement, persist or return prior result when duplicate final submit repeats after replacement booking creation.
- Tests:
  - duplicate confirm/cancel/rearrange/release calls do not duplicate calendar events, lifecycle events, notifications, or downstream messages
  - concurrent calls respect row version/get-for-update behaviour

### Commit 10: `test(booking): cover shared workflow behaviour across actors`

- Add matrix tests for self-service, LeadTech, admin, adviser approval, and system jobs.
- Keep endpoint tests thin: verify auth/token/permission mapping and DTO shape.
- Add application workflow tests for business behaviour and side effects.

## Test Plan

Targeted tests:

- `dotnet test tests/AFH.Booking.Tests/AFH.Booking.Tests.csproj --filter "Booking|Cancel|Rearrange|Lifecycle|Notification"`
- Add new tests as each refactor lands:
  - `BookingActorContext` mapping tests
  - lifecycle recorder sequencing tests
  - hold created/released/expired audit tests
  - public self-service error mapping tests
  - idempotency duplicate-submit tests

Existing tests to preserve:

- `LifecycleOrchestratorSequencingTests`
- `ConfirmBookingServiceTests`
- `CreateBookingServiceTests`
- `BookingCalendarServiceTests`
- `NoShowBookingServiceTests`
- `SelfServiceFunctionTests`
- `SelfServiceJourneyApplicationTests`
- `BookingOpenApiDocumentFactoryTests`
- `BookingRbacAuthorisationTests`
- notification policy/outbox tests

Manual smoke after implementation:

- Create hold creates a tentative calendar event and lifecycle hold event.
- Confirm hold updates calendar event to busy and sends booking confirmed notification.
- Self-service cancel validates token and uses shared cancellation workflow.
- LeadTech cancel uses same cancellation workflow with LeadTech actor/source.
- Self-service rearrange options for remote and in-person bookings load correctly.
- Final self-service rearrange submits only `newSlotId` and resolves option transaction internally.
- Cleanup job records hold expiry distinctly from manual release.
- No-show writes expected lifecycle event and any agreed notification policy outcome.

## Risks and Migration Notes

- Lifecycle reporting may already assume lifecycle starts at `Booked`. Treat hold events carefully and consider event-only hold audit before adding hold states.
- Rearrangement has the highest transactional risk because it creates and confirms a replacement booking before cancelling the old booking. Preserve the current sequencing test that prevents old-booking cancellation when replacement confirmation fails.
- Do not expose availability option transaction ids or require `newSlotTransactionId` from self-service callers.
- Keep client/prospect lookup for in-person rearrangement tied to the original booking transaction ref, not option transactions.
- Keep self-service public responses generic enough to avoid leaking internal slot/transaction details.
- Notification policy should decide channels and recipients, but booking workflows should decide which lifecycle outcome occurred.
- Calendar failures need explicit policy per workflow:
  - hold create/confirm can fail the workflow on conflict/provider failure
  - cancellation/release may choose to continue but must record failed Outlook steps
- Approval workflow currently records governance events in the lifecycle audit store. Formalise whether those are lifecycle events, governance events, or a separate category before expanding reporting.
- Migration should avoid changing external DTOs while actor context is introduced. Keep command compatibility shims until all endpoints and approval execution paths are migrated.

## Suggested Validation Commands

```bash
dotnet test tests/AFH.Booking.Tests/AFH.Booking.Tests.csproj --filter "Booking|Cancel|Rearrange|Lifecycle|Notification"
git diff --check
```
