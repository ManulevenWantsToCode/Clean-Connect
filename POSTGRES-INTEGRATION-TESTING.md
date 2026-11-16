# PostgreSQL integration tests

The PostgreSQL integration tests require a dedicated, migrated PostgreSQL database. They deliberately reject non-loopback hosts and database names that do not contain `test` (or contain `prod`/`live`). The tests create uniquely named temporary schemas and remove only those schemas when finished. They do not create or drop databases.

## Provisioning

Provision a **new, empty database** on a PostgreSQL instance you are authorized to use for development testing. Do not point this workflow at an application, staging, or production database. For isolation, a disposable local PostgreSQL cluster is preferred. When using a separately provisioned service, request a dedicated database and role from its administrator; do not change an existing service's configuration for this workflow.

Store the connection string in the current PowerShell process or in an untracked local secrets store. Do not commit it or place it in `appsettings.json`:

```powershell
$env:CLEAN_CONNECT_POSTGRES_TEST = 'Host=localhost;Port=5432;Database=clean_connect_integration_test;Username=<dedicated-user>;Password=<secret>;Include Error Detail=false'
```

Use a unique database name containing `test`, a loopback host, and credentials restricted to that test database. Keep `CLEAN_CONNECT_POSTGRES_TEST` and `ConnectionStrings__DefaultConnection` separate; the runner temporarily maps the former to the EF design-time factory and restores the prior process value afterward.

## Run

From the repository root, after provisioning the empty test database:

```powershell
./scripts/run-postgres-integration-tests.ps1
```

The runner applies the complete EF migration chain, checks `has-pending-model-changes`, and executes the tests whose names include `PostgreSql`. This covers booking concurrency and client-scoped idempotency, payment idempotency/webhook deduplication/claiming, database paging, and audit/business transaction behavior. To run without applying migrations (for a database already migrated by a separate controlled step), use:

```powershell
./scripts/run-postgres-integration-tests.ps1 -SkipMigration
```

The runner does not drop or reset the database. Tests clean up their own unique schemas. If a test process is forcibly terminated, inspect and remove only that run's `cc_*_test_<guid>` schema after confirming no test process is still using it.

## Current environment result

PostgreSQL 18.3 binaries are installed, but `initdb` cannot start a sandboxed cluster in the current environment: it reports Windows restricted-token error 87, then re-execution error 3, and fails to create the requested data directory. Docker and Podman are unavailable, and no WSL distribution is installed. The existing PostgreSQL service was not used or changed. Until a dedicated test database is provisioned in an environment that can run PostgreSQL, the four integration tests remain skipped and cannot be reported as PostgreSQL-verified.
