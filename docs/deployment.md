# Deployment

## Central backend (NestJS + MongoDB)

- Run the API as a container (Docker) or Windows/Linux service.
- MongoDB: **MongoDB Atlas** (managed) or a dedicated VM (self-hosted).

Environment (`central-backend/.env`):

- `MONGO_URI` — central database (e.g. `mongodb://localhost:27017/rr_bridal_central`)
- `PORT` — default `3000`
- `JWT_SECRET` — required for auth
- `RAZORPAY_KEY_ID`, `RAZORPAY_KEY_SECRET` — if using Razorpay
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

## Store billing client

The `store-billing-wpf` application has been removed from this repository. Only the central backend deployment steps above apply here.
