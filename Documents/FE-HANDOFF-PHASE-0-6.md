# FE handoff — Backend Phases 0–6 (Development Complete)

## Locked FE answers

| Topic | Value |
|-------|--------|
| `accessToken` TTL | **15 minutes** (`Jwt:AccessTokenMinutes`) |
| `refreshToken` TTL | **7 days** (`Jwt:RefreshTokenDays`) |
| CORS origins | `http://localhost:3000`, `http://127.0.0.1:3000`, `http://localhost:5173`, `http://127.0.0.1:5173`, `http://45.139.11.108`, `http://45.139.11.108:80` |
| CORS credentials | `AllowCredentials()` enabled (`Access-Control-Allow-Credentials: true`) — matches FE `withCredentials` / credentials:`include` |
| Attachment max size | **5 MB** (`Attachments:MaxFileSizeBytes`) |
| Allowed MIME types | `image/png`, `image/jpeg`, `application/pdf`, `text/plain` |
| Swagger (Development) | `/swagger` |
| Route prefixes | `/admin`, `/pm`, `/agent`, `/cm`, `/requester` under `/api/v1` |
| Realtime | **Polling only** — no SignalR in MVP |

## Demo credentials (Phase 6 seed)

Password for all demo users: `ChangeMe123!`

| Role | Username |
|------|----------|
| SuperAdmin | `superadmin` |
| ProviderManager | `demo.pm` |
| Agent | `demo.agent1`, `demo.agent2` |
| ClientManager | `demo.cm` |
| Requester | `demo.req1`, `demo.req2` |

## Status

- Backend **Development Complete** for B-Prep…6 on branch `cursor/all-phases-3282`.
- **API Verified** requires separate Postman/DB approval (phase-cycle Stage 2).
