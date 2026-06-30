# Salesmen API

Store-scoped salesman master data for POS billing and reporting.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/salesmen?storeId=` | List salesmen for a store (active and inactive) |
| `POST` | `/api/salesmen` | Create salesman |
| `GET` | `/api/salesmen/:id` | Get salesman by Mongo `_id` |
| `PATCH` | `/api/salesmen/:id` | Update name, phone, or active flag |

### Query parameters (`GET`)

| Param | Required | Description |
|-------|----------|-------------|
| `storeId` | Yes | Store code |
| `search` | No | Filter by name, phone, or salesman code |

## Create body

```json
{
  "storeId": "STORE01",
  "name": "Ravi Kumar",
  "phone": "9876543210",
  "salesmanCode": "SM001"
}
```

- `name` and `storeId` are required.
- `salesmanCode` is optional; when omitted, central allocates the next code (`SM001`, `SM002`, …) scoped per store.
- `isActive` defaults to `true`.

## Update body

```json
{
  "name": "Ravi K.",
  "phone": "9876543210",
  "isActive": false
}
```

`salesmanCode` cannot be changed after create.

## Response fields

| Field | Description |
|-------|-------------|
| `_id` | Central Mongo id (stored on bills as `salesmanId`) |
| `storeId` | Store code |
| `salesmanCode` | Unique per store |
| `name` | Display name (stored on bills as `salesman`) |
| `phone` | Optional contact |
| `isActive` | Inactive salesmen are hidden from billing picker |
| `createdAt` / `updatedAt` | Timestamps |

## WPF sync

Store billing pulls `GET /api/salesmen?storeId=` on each sync run into local `store_salesmen`. Creates from the POS call `POST /api/salesmen` when online.

## Bills

Posted bills include:

```json
{
  "salesmanId": "674a1b2c3d4e5f6789012345",
  "salesmanCode": "SM001",
  "salesman": "Ravi Kumar"
}
```

Legacy bills may only have `salesman` (free-text name).
