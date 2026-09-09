# Store billing client download API

Download a **self-contained** Windows WPF billing package for a chosen store and POS counter. Choose **`format=exe`** (tiny zip: single-file EXE + `.env`) or **`format=zip`** (full folder + `.env`).

The admin SPA (**TruStock**) is not in this repo. Wire store + counter + format picker and a download button to this endpoint.

## Constraint

`central-backend` on **Linux AWS cannot run `dotnet publish`** for WPF. Ops publish on Windows/CI, deploy artifacts to the API host, then Nest only zips with a generated `.env`.

## Ops: publish and deploy artifacts

On a **Windows** machine with .NET 9 Desktop SDK:

```powershell
cd store-billing-wpf
.\scripts\publish-store-billing-client.ps1
# Outputs:
#   dist/billing-client-win-x64/          → format=zip (folder)
#   dist/billing-client-win-x64-single/   → format=exe (PublishSingleFile)
```

Copy both to the Linux API host (example):

```bash
rsync -av --delete ./dist/billing-client-win-x64/ user@api-host:/var/lib/rr-bridal/billing-client-win-x64/
scp ./dist/billing-client-win-x64-single/RRBridal.StoreBilling.App.exe \
  user@api-host:/var/lib/rr-bridal/billing-client-win-x64-single/RRBridal.StoreBilling.App.exe
```

Set on the API host (`central-backend/.env`):

| Variable | Purpose |
|----------|---------|
| `API_PUBLIC_ORIGIN` | **Preferred** deployment public API origin (also used for media URLs). Written into till `.env` as `CENTRAL_API_BASE` when `PUBLIC_CENTRAL_API_BASE` is unset |
| `PUBLIC_CENTRAL_API_BASE` | Optional override for till packages only |
| `BILLING_CLIENT_ARTIFACT_DIR` | Folder for `format=zip` (must contain `RRBridal.StoreBilling.App.exe`) |
| `BILLING_CLIENT_SINGLE_EXE_PATH` | Absolute path to PublishSingleFile EXE for `format=exe` |

If both public URL env vars are unset, the download uses the **request host** (Swagger / TruStock URL, including `X-Forwarded-*` behind ALB).

Nest does **not** compile the client on AWS.

## Endpoint

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/admin/stores/:code/billing-client` | Download package for store + counter |

Auth: Bearer JWT (same as other `/api/admin/stores` routes).

### Path / query

| Param | Required | Description |
|-------|----------|-------------|
| `code` | yes | Store code (path), e.g. `store-001` |
| `posCounter` | yes | Integer `1`–`99` (query). `1` = manager till |
| `format` | no | `zip` (default) or `exe` |

Examples:

```
GET /api/admin/stores/store-001/billing-client?posCounter=1&format=zip
GET /api/admin/stores/store-001/billing-client?posCounter=1&format=exe
Authorization: Bearer <accessToken>
```

### Response

- `Content-Type: application/zip`
- Filenames:
  - `format=zip`: `RRBridal-StoreBilling-<code>-counter-<nn>.zip` (full publish folder + `.env`)
  - `format=exe`: `RRBridal-StoreBilling-<code>-counter-<nn>-exe.zip` (single-file EXE + `.env` only)

Generated `.env` beside the EXE:

```
STORE_ID=<code>
DEVICE_ID=counter-<nn>
POS_COUNTER=<n>
CENTRAL_API_BASE=<PUBLIC_CENTRAL_API_BASE>
PREFER_CENTRAL_ONLINE=<store.preferCentralOnline>
```

Offline / ZeroTier Mongo settings are not included; add those manually if the store runs Offline mode.

### Errors

| Status | When |
|--------|------|
| `404` | Store code not found |
| `400` | Invalid `posCounter` or `format` |
| `503` | Missing public API base (no `API_PUBLIC_ORIGIN` / `PUBLIC_CENTRAL_API_BASE` / request host); missing artifact dir/EXE for zip; missing `BILLING_CLIENT_SINGLE_EXE_PATH` for exe |

## TruStock UI

On the Stores list or store detail toolbar:

1. Store selector (or current row’s store `code`)
2. Counter select (`1`–`3` common; allow `1`–`99`)
3. Format select: **EXE** (`format=exe`) or **Full zip** (`format=zip`)
4. **Download** button

### Blob download (browser)

```typescript
async function downloadBillingClient(
  storeCode: string,
  posCounter: number,
  format: 'exe' | 'zip',
  accessToken: string,
) {
  const params = new URLSearchParams({ posCounter: String(posCounter), format });
  const res = await fetch(`/api/admin/stores/${encodeURIComponent(storeCode)}/billing-client?${params}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!res.ok) throw new Error(await res.text());

  const blob = await res.blob();
  const disposition = res.headers.get('Content-Disposition') ?? '';
  const match = /filename="([^"]+)"/.exec(disposition);
  const suffix = format === 'exe' ? '-exe.zip' : '.zip';
  const filename =
    match?.[1] ??
    `RRBridal-StoreBilling-${storeCode}-counter-${String(posCounter).padStart(2, '0')}${suffix}`;

  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
```

### Till install

**`format=exe` (recommended for simple tills):**

1. Unzip — you get `RRBridal.StoreBilling.App.exe` and `.env` only.
2. Keep both in the same folder.
3. Run the EXE (self-contained; talks to `CENTRAL_API_BASE` from `.env`).

**`format=zip`:**

1. Unzip the full folder (EXE + many runtime DLLs + `.env`).
2. Keep all files together.
3. Run `RRBridal.StoreBilling.App.exe`.
