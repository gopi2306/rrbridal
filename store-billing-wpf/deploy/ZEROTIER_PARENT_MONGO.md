# ZeroTier parent-Mongo deployment (Option B)

Use this when the store is **Offline** (local Mongo + outbox sync) and every POS counter connects to a **parent system MongoDB** over ZeroTier (shared `STORE_MONGO_URI`).

## Online vs Offline (store-wide / .env)

| Mode | How counters connect | ZeroTier / parent Mongo |
| --- | --- | --- |
| **`.env` Online** (`PREFER_CENTRAL_ONLINE=true` or `STORE_POS_MODE=online`) | This till → `CENTRAL_API_BASE` only | **Not required.** Highest priority; Settings checkbox locked. |
| **Central Online** (`preferCentralOnline` on the store; .env unset) | All counters → `CENTRAL_API_BASE` | **Not required.** Inherited via `GET /api/sync/store-pos-mode`. |
| **Offline** (`PREFER_CENTRAL_ONLINE=false` / default) | Shared store Mongo + POS1 sync to central | Use Option B below when multi-counter |

### Online store `.env` (minimal)

```env
STORE_ID=store-001
DEVICE_ID=counter-01
POS_COUNTER=1
CENTRAL_API_BASE=http://bridaldev.rrbazaar.in
PREFER_CENTRAL_ONLINE=true
# STORE_MONGO_URI optional; Mongo gates skipped while Online
```
---

## Required `.env` pattern (Offline / Option B)

| Variable | Rule |
| --- | --- |
| `STORE_ID` | Same on all counters |
| `STORE_MONGO_URI` | Same on all counters — parent ZeroTier IP + shared DB name |
| `DEVICE_ID` | Unique per till (`counter-01`, `counter-02`, …) |
| `POS_COUNTER` | Unique integer (`1`, `2`, …) |
| `CENTRAL_API_BASE` | Parent/central HTTP API for sync/auth |
| `SYNC_INTERVAL_MINUTES` | Auto-sync on **counter 1 only** (default 5; `0` = off) |
| `STORE_MONGO_CONNECT_TIMEOUT_SECONDS` | Default `20` (ZeroTier-friendly) |
| `STORE_MONGO_SERVER_SELECTION_TIMEOUT_SECONDS` | Default `20` |
| `STORE_MONGO_SOCKET_TIMEOUT_SECONDS` | Optional |
| `STORE_MONGO_HEALTH_INTERVAL_SECONDS` | Default `45` — UI heartbeat |
| `STORE_MONGO_REQUIRE_READY` | `true` (default) = block app start + bill post until Mongo is up (**Offline only**). `false` = no gate |

Templates: `env.counter-01.example`, `env.counter-02.example`, `env.counter-03.example`.

## When to turn the gate on or off

| Setup | `.env` / mode |
| --- | --- |
| Central Online (direct API, all counters) | Enable Online in Settings; ZeroTier not needed |
| Parent Mongo over ZeroTier (POS1 / POS2), Offline | `STORE_MONGO_REQUIRE_READY=true` |
| Local Mongo only (no ZeroTier dependency), Offline | `STORE_MONGO_REQUIRE_READY=false` |

With Offline + `RequireReady=true`, startup waits for a successful Mongo `ping` and shows Retry / Exit if unreachable.
With Central Online, the app starts even if Mongo is down (chip shows **Mongo: not required (Online)**).

## Parent machine checklist (Offline Option B)

1. MongoDB `bindIp` includes `0.0.0.0` (or at least the ZeroTier interface), not only `127.0.0.1`.
2. Windows Firewall (or equivalent) allows inbound TCP **27017** on the ZeroTier adapter.
3. ZeroTier is Connected on parent and every counter; all members are Authorized.
4. From each counter, `ping <parent-zerotier-ip>` succeeds.
5. From each counter, `mongosh "mongodb://<parent-zerotier-ip>:27017/rr_bridal_store01"` (or Compass) connects.
6. Prefer a **Stable** ZeroTier managed IP so `.env` does not change after restarts.
7. Keep **POS counter 1** online — only it runs scheduled Offline Mongo sync and store-wide Online transfer completion.

## App behavior

- **Online:** skip Mongo ready gate; login via central; writes and Sync All use `/api/store-pos/*`; every till refreshes PC-local print/master configuration, while POS1 periodically completes awaiting transfers in both directions.
- **Offline → Online:** the app performs one final Offline sync before saving the Online flag. A failed flush leaves the till Offline.
- **Offline:** startup waits for Mongo `ping` when `STORE_MONGO_REQUIRE_READY=true`. Header chip shows `Mongo: Connected / Reconnecting / Offline`.
- Settings → Sync status includes Central Online + Mongo lines; Refresh re-pings.
- Offline bill post is blocked while Mongo is offline when `STORE_MONGO_REQUIRE_READY=true`.

## Validation

### Online

1. Set Central Online on POS1 (with central JWT) → store `preferCentralOnline=true`.
2. Restart POS2 without ZeroTier → inherits Online, skips Mongo gate, logs in via central.
3. Post bill on POS2 against central only.

### Offline Option B

1. Parent Mongo + ZeroTier up → POS1 and POS2 login and bill against the same DB.
2. Disconnect ZeroTier on POS2 → chip shows Offline; post bill shows a clear warning.
3. Restore ZeroTier → chip returns to Connected without reinstalling the app.
