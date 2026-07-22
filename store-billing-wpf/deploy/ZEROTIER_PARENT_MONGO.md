# ZeroTier parent-Mongo deployment (Option B)

Use this when every POS counter connects directly to the **parent system MongoDB** over ZeroTier (shared `STORE_MONGO_URI`).

## Required `.env` pattern

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
| `STORE_MONGO_REQUIRE_READY` | `true` (default) = block app start + bill post until Mongo is up. `false` = no gate (local / ZeroTier not used) |

Templates: `env.counter-01.example`, `env.counter-02.example`, `env.counter-03.example`.

## When to turn the gate on or off

| Setup | `.env` |
| --- | --- |
| Parent Mongo over ZeroTier (POS1 / POS2) | `STORE_MONGO_REQUIRE_READY=true` |
| Local Mongo only (no ZeroTier dependency) | `STORE_MONGO_REQUIRE_READY=false` |

With `true`, startup waits for a successful Mongo `ping` and shows Retry / Exit if unreachable.
With `false`, the app starts even if Mongo is down (health chip may still show Offline).

## Parent machine checklist

1. MongoDB `bindIp` includes `0.0.0.0` (or at least the ZeroTier interface), not only `127.0.0.1`.
2. Windows Firewall (or equivalent) allows inbound TCP **27017** on the ZeroTier adapter.
3. ZeroTier is Connected on parent and every counter; all members are Authorized.
4. From each counter, `ping <parent-zerotier-ip>` succeeds.
5. From each counter, `mongosh "mongodb://<parent-zerotier-ip>:27017/rr_bridal_store01"` (or Compass) connects.
6. Prefer a **Stable** ZeroTier managed IP so `.env` does not change after restarts.
7. Keep **POS counter 1** online — only it runs scheduled central sync.

## App behavior

- Startup waits for a successful Mongo `ping` **when** `STORE_MONGO_REQUIRE_READY=true`. If unreachable, shows Retry / Exit (Yes / No).
- Header chip shows `Mongo: Connected / Reconnecting / Offline` with the parent host.
- Settings → Sync status includes the same Mongo health line; Refresh re-pings.
- Bill post is blocked while Mongo is offline **when** `STORE_MONGO_REQUIRE_READY=true`.

## Validation

1. Parent Mongo + ZeroTier up → POS1 and POS2 login and bill against the same DB.
2. Disconnect ZeroTier on POS2 → chip shows Offline; post bill shows a clear warning.
3. Restore ZeroTier → chip returns to Connected without reinstalling the app.
4. POS1 auto-sync still hits `CENTRAL_API_BASE`; POS2 can Run sync once from Settings if needed.
5. Both counters see the same bills/stock (shared `STORE_MONGO_URI`).
