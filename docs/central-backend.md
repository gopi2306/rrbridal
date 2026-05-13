# Central backend (NestJS + MongoDB)

## Run locally
From `central-backend/`:

1) Create `.env` based on `.env.example`
2) Install dependencies (already done):
- `npm install`
3) Start:
- `npm run start:dev`

## Swagger
- Swagger UI is served at `/swagger`.

## Key endpoints (scaffold)
- `GET /health`
- `POST /auth/devices/register`
- `POST /auth/devices/login`
- `POST /sync/push`
- `GET /sync/pull`
- `GET /sync/health`

