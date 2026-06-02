# Testing & acceptance criteria

## Central backend

- **Build**: `npm run build` succeeds
- **Health**: `GET /health` returns `{ ok: true }`
- **Device registration**
  - `POST /auth/devices/register` creates/returns device
  - `POST /auth/devices/login` validates deviceSecret
- **Sync push idempotency**
  - push same `eventId` twice → 1st `applied`, 2nd `duplicate`
- **Sync pull cursoring**
  - first pull with `sinceCursor=0` returns ordered updates
  - next pull with returned `cursor` returns no duplicates

## Store client (offline-first)

- **Outbox**: creating a local bill/payment should create an outbox event with `status=pending`
- **Sync**
  - when central is reachable, a sync run changes pending outbox events to `synced`
  - when central is not reachable, the app remains usable and pending outbox grows

## Payments

- **Provider selection**: Pine Labs and Razorpay selectable per payment
- **Retry safety**: duplicate sync sends do not duplicate central records (idempotency)

## Manual test script (happy path)

1. Start local central MongoDB.
2. Start central backend: `npm run start:dev`.
3. Start local store MongoDB.
4. Run store client.
5. Create a bill/payment → verify pending outbox increments.
6. Run sync once → verify pending outbox goes down.
7. Repeat steps 5–6 offline (stop central) then online (restart central).
