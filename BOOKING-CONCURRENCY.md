# Booking concurrency and idempotency

## Current reservation lifecycle and assumptions

The current product creates a booking for a selected Worker and sets it to `Pending`; that is the point where a Worker becomes reserved. The Worker acceptance command changes the status to `AcceptedAwaitingPayment` but keeps the same Worker and schedule. Admin assignment is allowed only while a booking is `Pending` and changes its assigned Worker. The domain and controllers contain no booking reschedule operation, and `WorkerId` is currently required in the database and booking creation command. Therefore this change does not introduce unassigned bookings or a rescheduling workflow.

`Pending`, `AcceptedAwaitingPayment`, `MarkAsPaid`, `AwaitingClientStartConfirmation`, `InProgress`, and `AwaitingClientConfirmation` reserve a Worker. `Rejected`, `Expired`, `Completed`, and soft-deleted bookings release the reservation. Intervals use `[start,end)`, so adjacent bookings are allowed. Booking dates with an unspecified `DateTimeKind` follow the application's existing convention and are interpreted as UTC; local-kind values are converted to UTC before persistence.

## Database protection

Migration `20261110000100_AddBookingConcurrencyAndIdempotency` enables PostgreSQL `btree_gist`, checks for invalid intervals and existing active overlaps, adds a UTC timestamp range exclusion constraint scoped to active, non-deleted bookings, and adds the idempotency columns and unique `(ClientId, IdempotencyKey)` index. It does not repair or delete existing rows. If preflight finds conflicts or invalid intervals, the migration aborts with an aggregate count; resolve those bookings through an approved operational process and retry. The migration needs permission to install `btree_gist` (or a DBA must install it first).

Apply the migration after a database backup and before deploying code that reads the new columns. The exclusion constraint is the concurrency authority for Client creation, Worker acceptance, and Admin assignment. EF persistence translates PostgreSQL exclusion violations to a safe booking conflict; HTTP/API callers receive a conflict response. Status updates to rejected, expired, completed, or deleted stop matching the partial constraint predicate and release the slot.

Rollback requires deploying the prior application version first, then rolling back the migration. This removes idempotency history and the database guard; do not roll back while the new application version is running.

## Booking idempotency

The API accepts `Idempotency-Key` as a request header. The MVC form carries a generated key in a hidden field. Keys are scoped by authenticated Client ID and persisted with a SHA-256 hash of the normalized booking payload on the booking row. A matching retry returns the original booking ID and does not repeat booking notification side effects. A key reused with a different payload returns HTTP 409. If creation fails before commit, neither booking nor key is persisted, so a retry can recover. Payment initialization, refunds, and payouts use separate workflows and are not made idempotent by this change. The application-level check distinguishes a conflicting payload; PostgreSQL's unique index arbitrates simultaneous uses of a key and the overlap exclusion constraint arbitrates schedule conflicts.

## PostgreSQL integration verification

Set `CLEAN_CONNECT_POSTGRES_TEST` to a disposable PostgreSQL database when running tests. The test refuses non-loopback hosts and database names that do not contain `test` or look production-like. Apply migrations to that dedicated database first with `ConnectionStrings__DefaultConnection` set to the same test connection. The integration test verifies the migrated production table has the exclusion/check constraints, idempotency index, and `btree_gist`, then creates a unique schema by cloning the migrated `Bookings` table and its indexes/constraints. It exercises concurrent inserts and assignments, adjacent intervals, release after rejection/completion, and Client-scoped idempotency. It drops its schema on completion. Without the test connection setting, the integration test is skipped; unit tests alone do not prove the production database migration has been applied or the constraint is active.

For local PostgreSQL, use a dedicated database such as `clean_connect_booking_concurrency_test`, never the application's Development or Production database. Set the connection with PowerShell process environment variables (do not commit it):

```powershell
$env:ConnectionStrings__DefaultConnection = "<dedicated local PostgreSQL test connection>"
$env:CLEAN_CONNECT_POSTGRES_TEST = $env:ConnectionStrings__DefaultConnection
dotnet ef database update --project Src/Clean-Connect.Persistence --startup-project Src/Clean-Connect.Persistence --no-build
dotnet test Tests/Clean-Connect.Application.Tests --no-restore
```
