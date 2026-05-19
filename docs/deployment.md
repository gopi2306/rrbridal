# Deployment

## Central backend (NestJS + MongoDB)

- Run the API as a container (Docker) or Windows/Linux service.
- MongoDB: **MongoDB Atlas** (managed) or a dedicated VM (self-hosted).

Environment (`central-backend/.env`):

- `MONGO_URI` — central database (e.g. `mongodb://localhost:27017/rr_bridal_central`)
- `PORT` — default `3000`
- `JWT_SECRET` — required for auth
- `RAZORPAY_KEY_ID`, `RAZORPAY_KEY_SECRET` — if using Razorpay

Run:

```bash
cd central-backend
npm install
npm run build
npm run start
```

Seed company profile and users:

```bash
cd central-backend
$env:SEED_FORCE_COMPANY_PROFILE='true'
npm run seed
```

---

## Store billing (WPF) — single PC

- Install the published WPF app.
- Point `.env` at local or remote MongoDB and central API.

Environment (`.env` beside the app or in project root for dev):

| Variable | Purpose |
|----------|---------|
| `STORE_ID` | Must match central store `code` (e.g. `store-001`) |
| `DEVICE_ID` | Unique till id (e.g. `counter-01`) |
| `POS_COUNTER` | Receipt counter label (e.g. `1`, `2`, `POS2`) |
| `STORE_MONGO_URI` | Local/store MongoDB (default `mongodb://localhost:27017/rr_bridal_store`) |
| `CENTRAL_API_BASE` | Central API URL |
| `SYNC_INTERVAL_MINUTES` | Auto central sync interval in minutes (**counter 1 / `POS_COUNTER=1` only**; default `5`, `0` = off) |

---

## Multi-counter LAN (shared MongoDB)

Use **one MongoDB server** on the store LAN and **one WPF install per billing counter**. All tills share inventory and bills; each till has a unique `DEVICE_ID` and `POS_COUNTER`.

```text
                    ┌─────────────────┐
                    │  Central API    │
                    │  (cloud/LAN)    │
                    └────────┬────────┘
                             │
     ┌───────────────────────┼───────────────────────┐
     │                       │                       │
┌────▼────┐            ┌─────▼─────┐           ┌─────▼─────┐
│ Counter │            │  Counter  │           │  Counter  │
│  PC 1   │            │   PC 2    │           │   PC 3    │
│ WPF+env │            │  WPF+env  │           │  WPF+env  │
└────┬────┘            └─────┬─────┘           └─────┬─────┘
     │                       │                       │
     └───────────────────────┼───────────────────────┘
                             │
                    ┌────────▼────────┐
                    │ MongoDB server  │
                    │ rr_bridal_store │
                    └─────────────────┘
```

### Store Mongo server (once per shop)

1. Install MongoDB Community on a dedicated PC (e.g. `192.168.1.10`).
2. Configure `bindIp` for the store subnet and enable authentication.
3. Create database user with read/write on `rr_bridal_store`.
4. Open firewall **TCP 27017** only to counter PCs.

Example URI for all counters:

`STORE_MONGO_URI=mongodb://rrstore:PASSWORD@192.168.1.10:27017/rr_bridal_store`

### Each counter PC

1. Publish WPF: `dotnet publish -c Release` from `store-billing-wpf`.
2. Copy the publish folder to the till PC.
3. Copy the matching env template from `store-billing-wpf/deploy/` to `.env` in the app folder (rename from `env.counter-01.example`).
4. Ensure **unique** `DEVICE_ID` and `POS_COUNTER` per PC; **same** `STORE_ID` and `STORE_MONGO_URI`.
5. First run: store user login → Settings → central login → Run sync once.
6. Post a test bill; confirm in Mongo `store_bills` has `deviceId`, `posCounter`, and bill no format `yyyyMMdd-{POS}-{seq}`.

### Env templates

See:

- [`store-billing-wpf/deploy/env.counter-01.example`](../store-billing-wpf/deploy/env.counter-01.example)
- [`store-billing-wpf/deploy/env.counter-02.example`](../store-billing-wpf/deploy/env.counter-02.example)
- [`store-billing-wpf/deploy/env.counter-03.example`](../store-billing-wpf/deploy/env.counter-03.example)

### Network checklist

- All counters resolve `CENTRAL_API_BASE` (hostname or IP).
- All counters reach the store Mongo host on port 27017.
- Same `STORE_ID` on every till at that store.
- **Unique** `DEVICE_ID` on every till.

### Reporting in the app

- **Dashboard** and **Ledger** default to **This counter** (bills tagged with this till’s `deviceId`).
- Switch to **Store-wide** to see all counters (legacy bills without `deviceId` appear only in store-wide view).

---

## MongoDB local service (optional, single-till dev)

Scripts can install MongoDB as a Windows service on a dev PC. For production multi-counter, prefer one shared store server instead of per-PC MongoDB.
