# Operations and observability

## Middleware order

Both hosts establish a validated `X-Correlation-ID` before request handling and run exception handling before routing. Forwarded headers are accepted only from ASP.NET Core's trusted proxy list. API ordering is HTTPS redirection, routing, CORS, authentication, rate limiting, then authorization. MVC keeps its error-page/status-code re-execution flow, with authentication before rate limiting and authorization. Health endpoints are mapped as anonymous minimal endpoints.

## Errors

Unhandled API exceptions use `application/problem+json`. Validation maps to 400, authorization to 403, missing resources to 404, and booking/payment/idempotency/concurrency conflicts to 409. Unexpected errors return a generic 500 with trace and correlation IDs; full exception details are written only to server logs. Expected request failures are logged as status/category without exception text. MVC continues to render its existing generic error view.

## Rate limits

Policies are fixed-window, do not queue requests, and return 429 with `Retry-After`:

| Policy | Default | Applied to |
| --- | --- | --- |
| `authentication` | 10 requests / 60 seconds | Login, registration, email confirmation/resend, password reset |
| `sensitive` | 30 / 60 seconds | Client payment initialization and Worker payout initiation |
| `public-read` | 120 / 60 seconds | Public service-type lookup, worker discovery, and coupon lookup/validation |

Limits are configurable using standard .NET configuration keys such as `RateLimiting__Authentication__PermitLimit` and `RateLimiting__Authentication__WindowSeconds`. Equivalent `Sensitive` and `PublicRead` keys are available. Anonymous callers are partitioned by the TCP peer IP; authenticated callers use the validated Identity subject. Client-supplied identity headers are ignored.

In production behind a reverse proxy, set `ForwardedHeaders__KnownProxies__0` to each trusted proxy IP and keep the proxy chain bounded with `ForwardedHeaders__ForwardLimit`. Never trust arbitrary forwarded headers. Built-in limits are process-local; multi-instance deployments should enforce matching or stricter aggregate quotas at a trusted gateway/WAF if per-user cluster-wide quotas are required. Tune defaults from observed traffic before rollout.

## Health checks

- `GET /health/live` reports process liveness and does not call dependencies.
- `GET /health/ready` checks database connectivity with a lightweight `CanConnect` probe.

Responses contain only `status`; database names, exception text and check details are not exposed. Paystack, email, and chat availability are not readiness dependencies because the application can continue serving core booking and account functions during provider outages.

## Correlation and logs

`X-Correlation-ID` is accepted only when it is at most 64 ASCII letters/digits, hyphens, or underscores. Otherwise the app generates a random ID. The response always returns the resulting ID, and a structured logging scope carries it through the request. Periodic hosted-service iterations receive their own generated correlation ID and operation name. Credentials, bearer tokens, payment payloads/signatures, and sensitive customer values must not be added to log templates. Keep console/file log access restricted and use centralized retention controls in production.

## Audit trail

The `AuditRecords` table records created, updated, and deleted states for bookings, payments, escrows, worker bank-detail entities, client/worker profiles, coupons, platform settings, and Identity users, plus login success/failure events. Records contain actor ID/kind (from authenticated claims, `Anonymous` for unknown login attempts, or `System`), action, entity type/ID, UTC timestamp, correlation ID, and changed property names. Old/new values, emails, bank values, password hashes, security stamps, phone numbers, and usernames are not persisted. The EF context rejects tracked update/delete attempts against audit rows; no user-facing mutation/read endpoint is exposed.

Audit entries are added in the same `SaveChanges` transaction as tracked business changes, so failed writes do not leave an audit row. Direct SQL, bulk `ExecuteDelete`, or a database owner can bypass application checks. Production database credentials should therefore follow least privilege, audit rows should be excluded from normal user-facing repositories, and database backups/retention should follow the organization's legal and operational retention schedule. The application role needs insert/select on this table; deployments should prevent ordinary operator credentials from updating or deleting it.

Pending audit rows are reused when the same `DbContext` retries `SaveChanges`, avoiding a second audit record for the same still-tracked change. The login writer uses a fresh context and the app does not configure automatic EF transient retries; each separately invoked login attempt is a distinct event. The MVC sign-in cookie is issued only after the success audit row is persisted. If automatic/replayed login writes are added later, use a server-generated event idempotency key rather than the client-supplied correlation ID.

The schema changes are `OperationalAuditTrail` and `WebhookCorrelationId`; apply them using the normal controlled migration procedure before deploying code that writes audit records or expects durable webhook correlation. If an application release must be rolled back, roll back the application artifact while leaving the database at its newer schema; the previous application version should continue to run against the additive tables/nullable column. Do not downgrade through `OperationalAuditTrail`: its down operation deliberately refuses to drop the audit table. If schema removal is ever required, first export and verify the retained audit history under the approved retention procedure, then perform a separate reviewed archival/removal migration.

## Verification notes

Use a disposable PostgreSQL database for persistence tests. Set `CLEAN_CONNECT_POSTGRES_TEST` only to a loopback database whose name contains `test`; never use production credentials/data. Rate limits are in-process and are not a substitute for edge protection against distributed abuse. Readiness currently covers PostgreSQL only; external provider health is intentionally represented by provider-specific operational metrics/alerts rather than a hard traffic gate.
