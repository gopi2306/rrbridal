# TruBilling React (Online POS)

Online-only browser POS that talks to central-backend `/api/pos-v2/*`.

## Stack

- Vite + React + TypeScript
- React Router
- Central NestJS `PosV2Module` (additive; does not change WPF `/api/sync`)

## Setup

```bash
# central-backend
cd central-backend
npm run start:dev

# UI
cd store-billing-react/ui
cp .env.example .env
npm install
npm run dev
```

Open http://localhost:5173

## Env

| Variable | Default | Purpose |
|----------|---------|---------|
| `VITE_API_BASE` | `http://localhost:3000/api` | Central API base |

Login requires a store user (`role: store`) and `storeId`.
