# Resource limits (central API)

Singleton settings in MongoDB collection `resource_limits` (`settingsKey: 'default'`). Role quotas live separately in auth settings. Requires JWT with role **`super_admin`** for admin endpoints.

## Limits

| PATCH field | Stored field | Enforced when |
|-------------|--------------|---------------|
| `stores` | `maxStores` | Creating a store; reactivating a store (`status: active`) |
| `warehouses` | `maxWarehouses` | Creating/activating a location with `type: warehouse` and `isActive: true` |
| `maxUsersPerStore` | `maxUsersPerStore` | **Per `storeId`**: creating/updating users with `role: store`, `locationKind: store`, status `active` or `invited` |
| `maxUsersPerWarehouse` | `maxUsersPerWarehouse` | **Per `warehouseLocationCode`**: same pattern for warehouse-scoped users |
| `users` | auth role quotas | Global cap per role (see below) |

Defaults: 3 stores, 5 warehouses, 20 users per store, 20 users per warehouse.

### Per-store vs global `users.store`

- **Site-scoped store users** (`role: store`, `locationKind: store`, with `storeId`) are limited only by `maxUsersPerStore` for that store—not by the global `users.store` role quota.
- **Site-scoped warehouse users** (`role: warehouse`, `locationKind: warehouse`) use `maxUsersPerWarehouse` per site, not the global `users.warehouse` quota.
- PATCH `users.store` / `users.warehouse` apply only to users **without** that site scope (if any exist).

Warehouses are **global** location records, not tied to a store code.

## Admin API

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/admin/resource-limits` | Current usage vs limits (includes per-store and per-warehouse user counts) |
| `PATCH` | `/api/admin/resource-limits` | Update one or more limits |

### GET response (excerpt)

```json
{
  "stores": {
    "limit": 3,
    "current": 2,
    "maxUsersPerStore": 20,
    "usersByStore": [
      { "storeId": "store-001", "current": 6, "limit": 20 },
      { "storeId": "store-002", "current": 0, "limit": 20 }
    ]
  },
  "warehouses": {
    "limit": 5,
    "current": 1,
    "maxUsersPerWarehouse": 20,
    "usersByWarehouse": [
      { "warehouseLocationCode": "wh-main", "current": 3, "limit": 20 }
    ]
  }
}
```

`usersByStore` lists every **active** store plus any store that still has assigned users. `usersByWarehouse` lists active warehouse locations the same way.

### Example PATCH

```json
{
  "stores": 5,
  "warehouses": 8,
  "maxUsersPerStore": 25,
  "maxUsersPerWarehouse": 15,
  "users": { "admin": 10 }
}
```

PATCH rejects lowering a limit below current usage. JSON `null` fields are ignored (treated as omitted).

## User assignment rules

- **Store user**: `role` must be `store`, `locationKind` must be `store`, `storeId` required when status is `active` or `invited`.
- **Warehouse user**: `role` must be `warehouse`, `locationKind` must be `warehouse`, `warehouseLocationCode` must reference an active warehouse location.

## Error messages

Operations return `400 Bad Request` when a limit is reached, for example:

- `Store limit reached (maximum N)...`
- `Store 'store-001' has reached the maximum of 20 users...`
- `Warehouse 'wh-main' has reached the maximum of 20 users...`
