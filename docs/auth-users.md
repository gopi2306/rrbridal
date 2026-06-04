# Users, roles, JWT login, and quotas

Central API authentication for **human users** (email + password) with **JWT** access tokens, **per-role user caps** (configurable), and **device** endpoints unchanged for store sync.

## Environment variables

| Variable | Purpose |
|----------|---------|
| `JWT_SECRET` | Signing secret for access tokens (set a long random value in production). |
| `JWT_EXPIRES_SEC` | Access token lifetime in **seconds** (default `28800` = 8 hours). |
| `AUTH_BOOTSTRAP_TOKEN` | Shared secret required in header `X-Auth-Bootstrap-Token` for `POST /auth/bootstrap` when the database has **no users yet**. |

## Bootstrap (first admin)

When `users` collection is empty:

```http
POST /auth/bootstrap
X-Auth-Bootstrap-Token: <same as AUTH_BOOTSTRAP_TOKEN>
Content-Type: application/json

{ "email": "owner@example.com", "password": "min8chars", "name": "Owner" }
```

Response includes `accessToken` and `user` (no password fields). After the first user exists, bootstrap returns `409`.

## Login

```http
POST /auth/login
Content-Type: application/json

{ "email": "owner@example.com", "password": "..." }
```

Only users with `status: active` receive a token. `invited` and `disabled` are rejected until a future invite flow is added.

JWT claims include: `sub`, `email`, `role`, `locationKind`, optional `storeId`.

Send `Authorization: Bearer <accessToken>` on protected routes.

## Role master

- Seeded collection `role_definitions`: `admin`, `warehouse`, `store`, `procurement`.
- `GET /roles` — public list for admin UI dropdowns.

## Quotas

- Document `auth_settings` (singleton `settingsKey: default`) holds `roleQuotas`, e.g. `{ "admin": 5, "warehouse": 1, "store": 5, "procurement": 5 }`.
- **Active** and **invited** users count toward the cap for their **role**; **disabled** users do not.
- Admins can adjust caps: `PATCH /admin/auth-settings` with body `{ "roleQuotas": { "store": 10 } }` (merged).

## Admin APIs (JWT + role `admin`)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/admin/users` | Create user (enforces quota). |
| `GET` | `/admin/users` | List users (passwords never returned). |
| `GET` | `/admin/users/me` | Current user profile. |
| `GET` | `/admin/users/:id` | Get one user. |
| `PATCH` | `/admin/users/:id` | Update fields; quota checked when role/status changes. |
| `POST` | `/admin/users/:id/disable` | Set `status` to `disabled`. |
| `GET` | `/admin/auth-settings` | Current merged quotas. |
| `PATCH` | `/admin/auth-settings` | Merge `roleQuotas`. |

`locationKind: store` requires a non-empty `storeId`.

### Store billing: max manual discount

Optional field on user documents: `maxDiscountPercent` (0–100). Caps **combined** manual discount on the store billing app: item discount % plus cash discount ₹, measured against the bill base after promotion/scheme discounts. Omitted in central = store app treats as **100** (no cap). Set via `POST /admin/users` or `PATCH /admin/users/:id`. Synced to tills on `GET /store-users` during store sync.

## Role access (screen permissions)

Collection `role_access`: `role`, `area`, `screen`, `allow`, `status` (`active` | `inactive`). One row per `(role, area, screen)`.

Admin APIs (JWT + role `admin` or `super_admin`):

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/admin/role-access` | Create or upsert one row (`role`, `area`, `screen`, `allow`, `status`). |
| `POST` | `/admin/role-access/filter` | Filter rows (`role`, `area`, `screen`, `allow`, `status`, pagination). |
| `GET` | `/admin/role-access/by-role/:role` | List active permissions for a role. |
| `PATCH` | `/admin/role-access/by-role/:role` | Bulk save `{ "permissions": [{ "area", "screen", "allow" }] }`. |
| `POST` | `/admin/role-access/by-role/:role/allow-all` | Set `allow: true` on all active rows for the role. |
| `GET` | `/admin/role-access/:id` | Get one row. |
| `PATCH` | `/admin/role-access/:id` | Update `allow` / `status`. |
| `DELETE` | `/admin/role-access/:id` | Soft-disable (`status: inactive`, `allow: false`). |

## Store client

- On startup, loads a local session file (if present) and sets `Authorization: Bearer` on the shared HTTP client.
- Main window: email + password fields (demo uses a plain `TextBox` for password; replace with a secure pattern for production), **Login** / **Logout**.
- Logout deletes the session file and clears the bearer header.

## Device auth (unchanged)

- `POST /auth/devices/register` and `POST /auth/devices/login` remain for device identity; they do not issue user JWTs in this iteration.
