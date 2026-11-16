# Payment Reliability

## Initialization

The API payment initialization routes require an `Idempotency-Key` header. The MVC booking form sends one with its payment POST. The key is scoped to a booking, and the database enforces a unique `(BookingId, IdempotencyKey)` pair plus one `Pending` payment per booking. The request hash covers the server-owned booking amount, normalized account email, payment method, and configured currency. A repeated key with a different hash returns HTTP 409.

The pending payment row, generated Paystack reference, and hash are persisted before the Paystack request. Initialization has its own `Pending`, `Initialized`, `Failed`, and `ReconciliationRequired` states. The checkout URL is persisted after Paystack returns it. If a retry finds an unresolved attempt, Clean Connect verifies that same reference with Paystack. A matching terminal success is routed through normal payment confirmation; a matching terminal failure closes the attempt; a still-pending transaction remains in reconciliation because verification does not return the original hosted checkout URL. Transport errors also leave the attempt in reconciliation. It never submits a second initialization request for an unknown outcome.

Paystack's Verify Transaction endpoint accepts the original transaction reference. A provider “not found” result marks the attempt failed, allowing a new client attempt with a new idempotency key; failed lookups that are unavailable or inconsistent remain in reconciliation. See [Paystack transaction verification](https://paystack.com/docs/api/transaction/).

The amount, currency, booking, and Client identity are obtained or checked server-side. The submitted amount, email, and user IDs are not used as payment authority.

## Webhook inbox and processing

The API validates Paystack's HMAC SHA-512 signature against the original request bytes. It stores a sanitized payload (event type, reference, amount, currency, status, provider ID/code) before running any business side effects. It does not store the signature or customer fields. Event IDs are derived from event type and provider event ID when present, with a stable event/reference/status/amount/currency fallback. A unique database index deduplicates deliveries.

An atomic database claim prevents simultaneous processors from applying the same event. Payment/booking/escrow updates and the inbox `Processed` state commit in one database transaction. Failed attempts persist total attempts, retry-cycle count, last/next attempt timestamps, and a bounded safe error category. The hosted recovery service reclaims expired processing leases after a crash. A failed event uses exponential backoff and moves to `Quarantined` when it reaches the configured maximum number of attempts; quarantined events are never automatically processed.

Configure `Paystack:WebhookRecovery` (`Paystack__WebhookRecovery__...` environment variables) with `PollIntervalSeconds` (30), `InitialDelaySeconds` (30), `MaximumDelaySeconds` (3600), `MaximumAttempts` (8 total attempts per recovery cycle), `ProcessingLeaseSeconds` (180), and `BatchSize` (25). The API's Admin-only `GET /api/v1/Payment/paystack/webhook/quarantined` endpoint exposes event metadata without the stored payload. `POST /api/v1/Payment/paystack/webhook/{eventId}/requeue` explicitly requeues one quarantined event, records the operator ID, and resets the retry-cycle count while preserving total attempts. Requeued events can quarantine again.

Charge success is verified directly with Paystack before the local transaction. Reference, amount, and currency must match the stored payment. Pending/failed events cannot reverse a successful payment. A successful payment creates/retains its escrow and notifications within the transaction. Transfer webhooks continue to be checked against the stored payout attempt and amount.

## Refunds and payouts

Refunds use a persisted reference derived from the payment ID (`refund-{paymentId}`), persisted before the provider call. A repeated reference is reconciled through Paystack's refund lookup endpoint, and the returned reference, amount, currency, and status must match before the existing attempt is treated as accepted. Local refund amount is bounded by the remaining refundable amount; current application behavior only requests one full-payment refund. A request acceptance leaves the local payment `Successful` and refund status `Pending` until the signed `refund.processed` event confirms completion. Failed or needs-attention refunds are not retried with a new reference. Admin booking refund now initiates this provider workflow; local booking/escrow state changes only after the signed completion webhook. [Paystack refund references and lookup](https://paystack.com/docs/payments/refunds/)

Payouts continue using the persisted escrow transfer reference. Unknown transfers are verified before retry; retries reuse the same reference. Paystack documents same-reference transfer retries to avoid a duplicate payment. [Paystack single transfers](https://paystack.com/docs/transfers/single-transfers/)

## Migration and deployment

Migration `20261111095410_AddPaymentIdempotencyAndWebhookInbox` adds payment idempotency/refund metadata and the durable webhook inbox. The follow-up recovery migration adds initialization state, refund totals, retry schedule, quarantine, and operator requeue metadata. Before adding the unique pending-payment index, the earlier migration checks for bookings that already have multiple pending payments and aborts with a count; it does not rewrite or delete existing payments. Resolve any such attempts deliberately, then rerun the migration.

The migration rollback removes inbox rows and the new payment metadata. Do not roll it back after the new application version has accepted payment requests or webhooks unless that data has been preserved and reconciled. Apply this migration only after reviewing the preflight on the target database. Ensure the configured webhook secret matches the Paystack dashboard secret and that the recovery hosted service is enabled in the API deployment.

For PostgreSQL concurrency tests, set `CLEAN_CONNECT_POSTGRES_TEST` to a dedicated loopback test database connection string whose database name contains `test` and whose migrations have been applied. Never point it at a production or live database. Each test uses a uniquely named schema and drops it afterward.

## Current limitations

- A Paystack reference verification can confirm a transaction but does not restore the original authorization URL. An ambiguous accepted initialization that remains pending is quarantined for operator reconciliation; Clean Connect will not generate a replacement reference automatically.
- The local webhook scheduler is durable across API instances through PostgreSQL atomic claims, but delivery/processing latency depends on the configured poll interval and lease duration.
- Refund partial amounts and arbitrary refund requests are not implemented; current refund workflows request the full original payment only. External refunds initiated directly in Paystack are outside the local refund balance and must be reconciled before issuing a local refund.
- Paystack enforces its own transfer balance. Clean Connect can prevent duplicate payouts per escrow/reference but does not reserve a merchant-wide Paystack balance across different escrows.
