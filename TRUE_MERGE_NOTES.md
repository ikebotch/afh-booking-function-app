# ACS true merge handoff

This package folds the staging AFH.Acs.Recorder feature surface into the root src architecture.

## What was merged into root
- Functions moved into `src/AFH.Acs.Function/Functions/V1/*` feature folders
- DTOs moved into `src/AFH.Acs.Contract/V1/Staging`
- staging services/models/helpers copied into root Application/Infrastructure folders
- old standalone `AFH.Acs.Recorder` source tree removed from this package

## Remaining work
- namespace unification and compile-time cleanup
- feature-by-feature wiring into Application/Infrastructure abstractions
- end-to-end tests for migrated endpoints
- OpenAPI/Scalar metadata expansion across the full merged surface
