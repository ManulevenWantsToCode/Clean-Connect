# API pagination

Collection endpoints use a shared page contract. Query parameters are `page` (default `1`) and `pageSize` (default `20`, maximum `100`). Values below one or a page size above the maximum are rejected with HTTP 400. Responses contain `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, and `hasNextPage`.

Example:

```http
GET /api/v1/Client/get-client-bookings-by-id?id={clientId}&page=2&pageSize=20&status=Completed&fromDate=2026-01-01&toDate=2026-01-31&sortBy=serviceDate&sortDirection=desc
```

`fromDate` and `toDate` are inclusive; date-only `toDate` values include the full day. Dates without a timezone are treated as UTC. Sort fields are restricted to each endpoint's documented allowlist, and results use an ID tie-breaker for stable ordering.

## Paginated endpoints

| Endpoint | Access | Filters | Sort fields |
|---|---|---|---|
| `GET /api/Client/get-client-bookings-by-id` | Client owner or Admin | status, serviceTypeId, workerId, searchTerm, date range | serviceDate, createdAt, amount, status |
| `GET /api/Worker/get-worker-booking-by-id` | Worker owner | status, serviceTypeId, clientId, searchTerm, date range | serviceDate, createdAt, amount, status |
| `GET /api/v1/Booking/admin` | Admin | status, serviceTypeId, clientId, workerId, searchTerm, date range | serviceDate, createdAt, amount, status |
| `GET /api/Client/all-clients` | Admin | state, searchTerm | name, createdAt |
| `GET /api/Worker/all-worker` | Admin | state, searchTerm, serviceTypeId, isAvailable | name, rating, hourlyRate |
| `POST /api/Worker/nearby` | Client | body location/radius/service type | distance, rating, worker ID |
| `GET /api/v1/Payment/Get-all-Payments` | Admin | status, bookingId, date range | createdAt, amount, status |
| `GET /api/v1/Payment/Get-Payments-By-Payment-Status` | Admin | status, bookingId, date range | createdAt, amount, status |
| `GET /api/v1/Payment/Get-Payments-By-BookingId` | Booking participant or Admin | date range | createdAt, amount, status |
| `GET /api/Coupon` | Admin | isActive | code, expirationDate, usage |

Service types remain a small bounded public lookup. Coupon code and validation routes return a single record/result. The quarantined webhook view remains capped at 100 safe metadata rows and is Admin-only. No API collection routes currently expose notifications, ratings, or chat messages; these remain in their existing MVC/realtime flows and were not given new public endpoints.

The Razor Client and Worker booking-history pages consume the same paged read repository and show Previous/Next links while preserving filters. This app has no separate browser API-service abstraction; other MVC admin lists still use their existing table behavior and are not consumers of the API envelopes.

No pagination migration was added. Existing primary/filter indexes were reviewed; the new implementation does not add duplicate indexes without query-plan evidence. API list responses now use the page envelope instead of bare arrays, so external API clients of these list routes must read `items` and page metadata. Internal Razor pages were updated where they consume booking history.
