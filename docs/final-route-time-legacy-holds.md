# Final Route-Time Legacy Holds

The final in-person route-time guard depends on exact coordinates persisted on the selected slot travel snapshot. New in-person slots receive those coordinates from the Location travel-coverage response during availability search.

Legacy in-person holds created before this snapshot shape may not have coordinates. The default behaviour is to block confirmation gracefully with `ExactRouteTimeUnavailable`; this avoids confirming a slot without the final exact check.

Rollout options:

- Temporarily set `FinalRouteTimeGuard:Enabled=false` to disable the guard globally during rollout.
- Temporarily set `FinalRouteTimeGuard:AllowLegacyMissingCoordinates=true` to allow only missing-coordinate legacy holds to continue while still checking new coordinate-backed holds.
- Backfill existing active in-person slots by resolving the stored client and adviser postcodes through Location, then writing `SourceLatitude`, `SourceLongitude`, `DestinationLatitude`, and `DestinationLongitude` on `BookingSlots`.

Recommended backfill scope: active, unconfirmed in-person holds and any open rearrangement holds. Online bookings do not require route-time coordinates.
