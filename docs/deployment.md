# Deployment

## Central backend (NestJS + MongoDB)

- Run the API as a container (Docker) or Windows/Linux service.
- MongoDB: **MongoDB Atlas** (managed) or a dedicated VM (self-hosted).

Environment (`central-backend/.env`):

- `MONGO_URI` — central database (e.g. `mongodb://localhost:27017/rr_bridal_central`)
- `PORT` — default `3000`
- `JWT_SECRET` — required for auth
- `RAZORPAY_KEY_ID`, `RAZORPAY_KEY_SECRET` — if using Razorpay
- `API_PUBLIC_ORIGIN` (preferred), optional `PUBLIC_CENTRAL_API_BASE`, `BILLING_CLIENT_ARTIFACT_DIR`, `BILLING_CLIENT_SINGLE_EXE_PATH` — store billing EXE/zip download; see [store-billing-client-download.md](store-billing-client-download.md)
- `BACKUP_*` — optional daily MongoDB backup; see [mongo-backup.md](mongo-backup.md)

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

## Store billing client (WPF)

The desktop POS lives in [`store-billing-wpf`](../store-billing-wpf/).

- **Publish (Windows/CI only):** `store-billing-wpf/scripts/publish-store-billing-client.ps1` → folder (`format=zip`) + PublishSingleFile EXE (`format=exe`).
- **Deploy artifacts** to the Linux API host paths used by `BILLING_CLIENT_ARTIFACT_DIR` and `BILLING_CLIENT_SINGLE_EXE_PATH`.
- **Download API:** `GET /api/admin/stores/:code/billing-client?posCounter=1&format=exe|zip` — see [store-billing-client-download.md](store-billing-client-download.md).

Till config is runtime `.env` beside the EXE (`CENTRAL_API_BASE`, `STORE_ID`, `POS_COUNTER`, …). The Nest API on Linux AWS does **not** run `dotnet publish`.

