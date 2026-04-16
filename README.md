# AFH ACS Function App

AFH ACS is the meeting-platform service for AFH. It owns meeting creation, ACS-issued meeting identity and join tokens, meeting links, meeting lifecycle lookups, recording abstractions, transcription workflows, and system docs/health endpoints.

It does not own booking workflows, adviser discovery, SharePoint/Graph lookup flows, or mandatory email delivery. Adviser display information is resolved from the Location service adviser coverage endpoint, and transcription flows are routed through the shared Speech AI SDK.

## Architecture

The source tree is organized into one active architecture:

- `src/AFH.Acs.Function`: thin Azure Functions entry points, middleware, request parsing, and OpenAPI/Scalar endpoints.
- `src/AFH.Acs.Contract`: versioned request and response DTOs only.
- `src/AFH.Acs.Application`: orchestration, service abstractions, and meeting/transcription workflows.
- `src/AFH.Acs.Domain`: core meeting entities and invariants.
- `src/AFH.Acs.Infrastructure`: persistence, ACS identity integration, Location adviser client, recording implementations, Speech AI adapter, and logging.

## Retained Endpoint Surface

- Meetings: create meeting, issue identity token, issue join token, record consent, get meeting by id, get meeting by group, create meeting link.
- Recordings: start, stop, list, get.
- Transcription: submit from meeting, status, files, transcript content, speaker-formatted transcript, cancel, delete.
- System: health, OpenAPI JSON, Scalar UI.

## Configuration

Copy `src/AFH.Acs.Function/local.settings.template.json` to `local.settings.json` and provide:

- `Acs:ConnectionString` for ACS identity/token flows.
- `MeetingDb:ConnectionString` or `MSSQL_CONN` for meeting persistence.
- `Frontend:JoinBaseUrl` for client/adviser meeting links.
- `Location:BaseUrl` and optional `Location:FunctionCode` for adviser info lookups.
- `Recording:Mode` with `Metadata` as the default and `LiveAcs` as the structured extension point.
- `ErrorEmail:Enabled=false` unless service-local handled error emails are explicitly wanted.

## Build And Test

Build the solution:

```bash
dotnet build AFH.Acs.sln -p:_FunctionsBuildEnabled=false
```

Run the focused ACS tests:

```bash
dotnet test tests/AFH.Acs.Tests/AFH.Acs.Tests.csproj -p:_FunctionsBuildEnabled=false
```

The `_FunctionsBuildEnabled=false` switch avoids a legacy Azure Functions extension metadata generator dependency on `.NETCore.App 2.0`, which is not present on current local toolchains. The function code itself still compiles successfully under the current isolated worker stack.
