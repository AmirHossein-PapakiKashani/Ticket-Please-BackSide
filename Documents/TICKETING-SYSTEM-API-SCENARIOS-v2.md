# 🔌 Ticketing System — Complete API Scenarios v2 (Contract Reference for Frontend)

> **Status:** ✅ Final — This document replaces `TICKETING-SYSTEM-SCENARIOS-v1.md` and its level of detail matches the API contract between backend and frontend.
> **Backend:** ASP.NET Core (.NET 10) — Web API + JWT Bearer Auth
> **Audience:** Frontend team — start implementing pages exactly based on the Endpoints, input/output models, and status codes in this document
> **Companion files:**
> - `openapi.json` → The same contract in OpenAPI 3.0 format, importable in Swagger UI / Postman
> - `TICKETING-SYSTEM-IMPLEMENTATION-PHASES-v1.md` → Frontend implementation phases
> - `TICKETING-SYSTEM-BACKEND-IMPLEMENTATION-GUIDE-v1.md` → Backend step-by-step build order (mirrors FE phases 0–6)

---

## 📖 Table of Contents

| Section | Title                                      | Endpoint Count |
| ------- | ------------------------------------------ | -------------- |
| 0       | Base Contracts (Auth, Pagination, Errors, Enums) | —          |
| 1       | Auth & Common (Section 10 of v6 doc)       | 14             |
| 2       | SuperAdmin Area (Section 11 of v6 doc)       | 12             |
| 3       | ProviderManager Area (Section 12 of v6 doc)  | 12             |
| 4       | Agent Area (Section 13 of v6 doc)          | 11             |
| 5       | ClientManager Area (Section 14 of v6 doc)    | 7              |
| 6       | Requester Area (Section 15 of v6 doc)      | 6              |
| 7       | Endpoint ↔ Role Mapping Summary            | —              |

**Total: 62 Endpoints**

---

## ⚙️ 0. Base Contracts (Read Before Starting)

### 0.1 Base URL and Versioning

```
Base URL:  https://api.ticketing.example.com/api/v1
```

All Routes in this document are written relative to this Base URL.

### 0.2 Authentication

- Method: **JWT Bearer**
- Header: `Authorization: Bearer {accessToken}`
- All Endpoints except `POST /auth/login`, `POST /auth/refresh-token`, `POST /auth/forgot-password`, `POST /auth/reset-password` require this header.
- User role (`roleName`) is included in the token payload as a claim and enforced on each Endpoint via `[Authorize(Roles = "...")]` (ASP.NET Core policy).
- `accessToken` expiry: short-lived (e.g., 15 minutes) → refreshed via `refresh-token`.

### 0.3 Pagination (for all 🔵 `[LIST]` Endpoints)

**Shared input parameters (Query String):**

| Field      | Type   | Required | Default | Description                              |
| ---------- | ------ | -------- | ------- | ---------------------------------------- |
| search     | string | ❌       | —       | Text search (based on the primary field of each list) |
| pageNumber | int    | ❌       | 1       | Page number, minimum 1                   |
| pageSize   | int    | ❌       | 20      | Maximum 100                              |

**Shared output envelope (`PagedResponse<T>`):**

```json
{
  "items": [ /* array of items for the same Endpoint */ ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 137,
  "totalPages": 7
}
```

### 0.4 Error Format (Error Envelope) — Based on standard ASP.NET Core `ProblemDetails`

Input validation errors (400) with `ValidationProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "username": ["Username is required."],
    "password": ["Password must be at least 6 characters."]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00"
}
```

Other errors (401/403/404/409/500) with `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this ticket.",
  "instance": "/api/v1/agent/tickets/55",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00"
}
```

> 💡 **Golden rule for frontend:** The frontend always decides based on the HTTP `status` code, not on the `title`/`detail` text (which may change). `detail` is only for display to the user. For 400 errors, always check the `errors` object to display the error under the corresponding Input field.

### 0.5 General Status Code Policy in This Document

| Status | When to Use |
| ------ | ----------- |
| 200    | Successful GET/LIST/UPDATE/PATCH operations (including Deactivate which is Soft, not a real Delete) |
| 201    | Successful creation of a new record (POST `[SET]`) — with `Location` header pointing to the newly created record |
| 400    | Invalid input (wrong format, required field empty, invalid Enum) → `ValidationProblemDetails` |
| 401    | Token missing/invalid/expired, or Login with wrong credentials |
| 403    | User is logged in but role/ownership is insufficient for this operation (e.g., Agent on a ticket not assigned to them) |
| 404    | Record not found **or** record belongs to another Tenant (different Provider/Client) — intentionally returned instead of 403 to prevent disclosing record existence |
| 409    | Uniqueness conflict (e.g., duplicate `username`) or attempt to transition to a disallowed state |
| 500    | Unexpected server error |

> ⚠️ **403 vs 404 difference in this document:** If the user has the correct role but the requested record belongs to another Tenant (e.g., one ProviderManager tries to view a Client belonging to another Provider) → **404** is returned. If the user is in the same Tenant but is denied due to internal ownership/role rules (e.g., Agent on a teammate's ticket they are not responsible for) → **403** is returned.

### 0.6 Enums (always serialized as string, not number)

| Enum               | Allowed Values                                                             |
| ------------------ | -------------------------------------------------------------------------- |
| `RoleName`         | `SuperAdmin`, `ProviderManager`, `Agent`, `ClientManager`, `Requester`     |
| `TicketStatus`     | `Open`, `InProgress`, `PendingRequesterResponse`, `Resolved`, `Closed`     |
| `TicketPriority`   | `Low`, `Medium`, `High`                                                    |
| `NotificationType` | `NewTicketAssigned`, `NewMessage`, `TicketReassigned`, `TicketResolved`, `TicketClosed`, `TicketReopened` |

### 0.7 Route Naming Convention

- Prefix by area (aligned with v6 document sections): `/auth`, `/profile`, `/layout`, `/notifications` (shared) → `/admin/*` (SuperAdmin) → `/pm/*` (ProviderManager) → `/agent/*` (Agent) → `/cm/*` (ClientManager) → `/requester/*` (Requester)
- Deactivate operations (🔴 symbol in v6) are implemented with the verb `PATCH .../deactivate`, not a real HTTP `DELETE` — because per Section 3 of the v6 document, everything uses Soft Delete/Deactivate, not physical deletion.
- 💡 Suggested .NET Controller structure: `AdminController`, `ProviderManagerController`, `AgentController`, `ClientManagerController`, `RequesterController`, `AuthController`, `ProfileController`, `NotificationsController` — each with Policy-based Authorization matching the role.

### 0.8 Shared DTO List (full definitions in `openapi.json` → `components.schemas`)

| Schema Name                | Used In                                  |
| -------------------------- | ---------------------------------------- |
| `PagedResponse`            | All 🔵 `[LIST]`                          |
| `SuccessResponse`          | `{ "success": true }` — simple Update response |
| `ProblemDetails`           | 401/403/404/409/500 errors               |
| `ValidationProblemDetails` | 400 errors                               |
| `LoginResponse`            | SC-10.1-01                               |
| `TokenResponse`            | SC-10.1-02                               |
| `UserProfile`              | SC-10.3-01                               |
| `ProfileSummary`           | SC-10.4-01                               |
| `SubscriptionInfo`         | SC-10.3-04                               |
| `NotificationItem`         | SC-10.5-01                               |
| `TicketDetail`             | SC-13.2-01 / SC-15.3-01                  |
| `TicketMessage`            | SC-13.2-02 / SC-15.3-02                  |
| `Attachment`               | Inside `TicketMessage`                   |
| `TicketNote`               | SC-13.2-08                               |

---

## 🔑 1. Auth & Common (Section 10 of v6 doc)

### 1.1 Login

#### 🟡 `POST /auth/login` — Login (SC-10.1-01)

**Role:** No Auth (Public)

**Input (Body):**

| Field    | Type   | Required | Validation Rule        |
| -------- | ------ | -------- | ---------------------- |
| username | string | ✅       | 3 to 100 characters    |
| password | string | ✅       | Minimum 6 characters   |

**Successful output — `LoginResponse` (200):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "8f14e45f-ceea-4c95-8f0f-9c1a4a3f2b1e",
  "userId": 12,
  "roleName": "Agent",
  "fullName": "Ali Mohammadi"
}
```

| # | Type | Condition (Input/State)                    | Status | Output                                                              |
| - | ---- | ------------------------------------------ | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Correct username/password + `IsActive=true` | 200    | `LoginResponse` above                                               |
| 2 | ⛔   | `username` does not exist                  | 401    | `ProblemDetails` — same message as row 3 ("Incorrect username or password") |
| 3 | ⛔   | Wrong `password`                           | 401    | Same message as row 2 (intentionally identical for security)        |
| 4 | ⛔   | `IsActive=false`                           | 403    | `ProblemDetails` — "Account has been deactivated"                 |
| 5 | ⛔   | `IsDeleted=true`                           | 403    | Same as row 4                                                       |
| 6 | ⛔   | `username` or `password` empty/too short   | 400    | `ValidationProblemDetails` → `errors.username` / `errors.password`  |

---

#### 🟡 `POST /auth/refresh-token` — Refresh Token (SC-10.1-02)

**Role:** No Auth (only with valid `refreshToken`)

**Input (Body):**

| Field        | Type   | Required |
| ------------ | ------ | -------- |
| refreshToken | string | ✅       |

**Successful output — `TokenResponse` (200):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "3c2d1a4e-5f6b-4a1c-9e8d-7f6a5b4c3d2e"
}
```

| # | Type | Condition                                        | Status | Output                                                        |
| - | ---- | ------------------------------------------------ | ------ | ------------------------------------------------------------- |
| 1 | ✅   | Valid and non-expired token                      | 200    | New `TokenResponse` (Rotation — previous token is invalidated) |
| 2 | ⛔   | Expired token                                    | 401    | `ProblemDetails` — "Your session has expired, please log in again" |
| 3 | ⛔   | Invalid/forged/already-used token (Rotation)     | 401    | Same message as above                                         |
| 4 | ⛔   | Empty `refreshToken`                             | 400    | `ValidationProblemDetails`                                  |

---

#### 🟠 `POST /auth/logout` — Logout (SC-10.1-03)

**Role:** All roles (requires Bearer Token)

**Input (Body):** `{ "refreshToken": "..." }`

**Successful output — `SuccessResponse` (200):**

```json
{ "success": true }
```

| # | Type | Condition                              | Status | Output                                              |
| - | ---- | -------------------------------------- | ------ | --------------------------------------------------- |
| 1 | ✅   | Token was valid and has been invalidated | 200  | `{ "success": true }`                               |
| 2 | ✅   | Token was already invalid/expired      | 200    | `{ "success": true }` (Idempotent — no error thrown) |
| 3 | ⛔   | `Authorization` header missing         | 401    | `ProblemDetails`                                    |

### 1.2 Forgot / Reset Password

#### 🟡 `POST /auth/forgot-password` — Request Reset (SC-10.2-01)

**Role:** No Auth

**Input (Body):** `{ "username": "string" }`

**Successful output — `SuccessResponse` (200):**

```json
{ "success": true }
```

| # | Type | Condition                              | Status | Output                                                              |
| - | ---- | -------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | `username` exists                      | 200    | `{ "success": true }` + SMS/email with token is sent                |
| 2 | ✅   | `username` does not exist              | 200    | `{ "success": true }` — 💡 intentionally same success response (prevents user enumeration) |
| 3 | ⛔   | Empty `username`                       | 400    | `ValidationProblemDetails`                                        |

---

#### 🟡 `POST /auth/reset-password` — Submit New Password (SC-10.2-02)

**Role:** No Auth (only with valid `token`)

**Input (Body):**

| Field       | Type   | Required | Rule               |
| ----------- | ------ | -------- | ------------------ |
| token       | string | ✅       | —                  |
| newPassword | string | ✅       | Minimum 6 characters |

**Successful output — `SuccessResponse` (200):** `{ "success": true }`

| # | Type | Condition                                       | Status | Output                                                    |
| - | ---- | ----------------------------------------------- | ------ | --------------------------------------------------------- |
| 1 | ✅   | Valid, non-expired, unused token                | 200    | `{ "success": true }` — password is changed             |
| 2 | ⛔   | Expired token (`ExpireAt < now`)                | 400    | `ProblemDetails` — "Recovery link has expired"            |
| 3 | ⛔   | Already-used token (`IsUsed=true`)              | 400    | `ProblemDetails` — "This link has already been used"      |
| 4 | ⛔   | Invalid/forged token                            | 400    | Same general message as above                             |
| 5 | ⛔   | `newPassword` shorter than allowed              | 400    | `ValidationProblemDetails` → `errors.newPassword`         |

### 1.3 Profile

#### 🟢 `GET /profile` — Get Profile (SC-10.3-01)

**Role:** All roles

**Successful output — `UserProfile` (200):**

```json
{
  "fullName": "Ali Mohammadi",
  "username": "ali.m",
  "phoneNumber": "09121234567",
  "roleName": "Agent",
  "orgName": "Nice Mind Tech Co."
}
```

| # | Type | Condition                  | Status | Output           |
| - | ---- | -------------------------- | ------ | ---------------- |
| 1 | ✅   | Valid token                | 200    | `UserProfile`    |
| 2 | ⛔   | Invalid/expired token      | 401    | `ProblemDetails` |

---

#### 🟠 `PUT /profile` — Update Profile (SC-10.3-02)

**Input (Body):**

| Field       | Type   | Required | Rule                        |
| ----------- | ------ | -------- | --------------------------- |
| fullName    | string | ✅       | 2 to 150 characters         |
| phoneNumber | string | ✅       | Iranian mobile number format |

**Successful output:** `{ "success": true }`

| # | Type | Condition                        | Status | Output                                              |
| - | ---- | -------------------------------- | ------ | --------------------------------------------------- |
| 1 | ✅   | Valid values                     | 200    | `{ "success": true }`                               |
| 2 | ⛔   | Invalid `phoneNumber` format     | 400    | `ValidationProblemDetails` → `errors.phoneNumber`   |
| 3 | ⛔   | Empty `fullName`                 | 400    | `ValidationProblemDetails` → `errors.fullName`    |

---

#### 🟠 `PUT /profile/change-password` — Change Password (SC-10.3-03)

**Input (Body):** `{ "currentPassword": "string", "newPassword": "string" }`

**Successful output:** `{ "success": true }`

| # | Type | Condition                                    | Status | Output                                              |
| - | ---- | -------------------------------------------- | ------ | --------------------------------------------------- |
| 1 | ✅   | Correct `currentPassword`                    | 200    | `{ "success": true }`                               |
| 2 | ⛔   | Wrong `currentPassword`                      | 401    | `ProblemDetails` — "Current password is incorrect"  |
| 3 | ⛔   | `newPassword` shorter than allowed           | 400    | `ValidationProblemDetails` → `errors.newPassword`   |

---

#### 🟢 `GET /profile/subscription` — Get Subscription Info (SC-10.3-04)

**Role:** `ProviderManager` only

**Successful output — `SubscriptionInfo` (200):**

```json
{
  "planName": "Gold Plan",
  "expireDate": "2026-12-01T00:00:00Z",
  "remainingDays": 147
}
```

| # | Type | Condition                                     | Status | Output                                              |
| - | ---- | --------------------------------------------- | ------ | --------------------------------------------------- |
| 1 | ✅   | ProviderManager role + active subscription    | 200    | `SubscriptionInfo`                                  |
| 2 | ⛔   | Role other than ProviderManager               | 403    | `ProblemDetails`                                    |
| 3 | ⛔   | Provider has no active subscription           | 404    | `ProblemDetails` — "No active subscription found"   |

### 1.4 Layout

#### 🟢 `GET /layout/profile-summary` — Get Profile Summary (SC-10.4-01)

**Successful output — `ProfileSummary` (200):**

```json
{ "fullName": "Ali Mohammadi", "roleName": "Agent", "avatarUrl": null }
```

| # | Type | Condition            | Status | Output           |
| - | ---- | -------------------- | ------ | ---------------- |
| 1 | ✅   | Valid token          | 200    | `ProfileSummary` |
| 2 | ⛔   | Invalid token        | 401    | `ProblemDetails` |

---

#### 🟢 `GET /layout/notifications/unread-count` — Get Unread Notification Count (SC-10.4-02)

**Successful output (200):** `{ "unreadCount": 4 }`

| # | Type | Condition                  | Status | Output                     |
| - | ---- | -------------------------- | ------ | -------------------------- |
| 1 | ✅   | Valid token                | 200    | `{ "unreadCount": <int> }` |
| 2 | ⛔   | Invalid token              | 401    | `ProblemDetails`           |

### 1.5 Notifications

#### 🔵 `GET /notifications` — Get Notifications List (SC-10.5-01)

**Input (Query):** `search?, pageNumber, pageSize, onlyUnread?: bool`

**Successful output — `PagedResponse<NotificationItem>` (200):**

```json
{
  "items": [
    {
      "id": 501,
      "type": "NewMessage",
      "title": "New message in ticket #55",
      "body": "Ali Mohammadi: Has the issue been resolved?",
      "refId": 55,
      "isRead": false,
      "createdAt": "2026-07-07T09:12:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 4,
  "totalPages": 1
}
```

| # | Type | Condition                        | Status | Output                                              |
| - | ---- | -------------------------------- | ------ | --------------------------------------------------- |
| 1 | ✅   | Valid input                      | 200    | `PagedResponse` above (`items: []` is possible)     |
| 2 | ⛔   | `pageSize > 100`                 | 400    | `ValidationProblemDetails`                          |
| 3 | ⛔   | `pageNumber < 1`                 | 400    | `ValidationProblemDetails`                          |

---

#### 🟠 `PATCH /notifications/{notificationId}/read` — Mark as Read (SC-10.5-02)

**Successful output:** `{ "success": true }`

| # | Type | Condition                                              | Status | Output                  |
| - | ---- | ------------------------------------------------------ | ------ | ----------------------- |
| 1 | ✅   | Notification belongs to the current user               | 200    | `{ "success": true }`   |
| 2 | ⛔   | `notificationId` does not exist or belongs to another user | 404 | `ProblemDetails`     |

---

#### 🟠 `PATCH /notifications/read-all` — Mark All as Read (SC-10.5-03)

**Successful output:** `{ "success": true }`

| # | Type | Condition                                                      | Status | Output                  |
| - | ---- | -------------------------------------------------------------- | ------ | ----------------------- |
| 1 | ✅   | Always succeeds (even if there are no unread notifications)    | 200    | `{ "success": true }`   |

---

## 🛡️ 2. SuperAdmin Area (Section 11 of v6 doc)

> All Endpoints in this section require `[Authorize(Roles="SuperAdmin")]`; any other role attempting access → always **403**.

### 2.1 Admin Dashboard

#### 🟢 `GET /admin/dashboard/stats` — Get Platform Stats (SC-11.1-01)

**Successful output (200):**

```json
{
  "totalProviders": 34,
  "activeProviders": 30,
  "totalClients": 210,
  "totalTicketsThisMonth": 1523,
  "revenueThisMonth": 458000000
}
```

| # | Type | Condition                  | Status | Output           |
| - | ---- | -------------------------- | ------ | ---------------- |
| 1 | ✅   | Always (SuperAdmin)        | 200    | Object above     |
| 2 | ⛔   | Role other than SuperAdmin | 403    | `ProblemDetails` |

### 2.2 Providers Manager

#### 🔵 `GET /admin/providers` — Get Providers List (SC-11.2-01)

**Input (Query):** `search?, pageNumber, pageSize`

**Successful output — `PagedResponse<ProviderListItem>`:**

```json
{
  "items": [
    { "id": 1, "name": "Nice Mind Tech Co.", "email": "info@nicemind.ir", "isActive": true, "planName": "Gold Plan", "clientCount": 12 }
  ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 34, "totalPages": 2
}
```

| # | Type | Condition                          | Status | Output                       |
| - | ---- | ---------------------------------- | ------ | ---------------------------- |
| 1 | ✅   | Valid input (with/without `search`) | 200   | `PagedResponse` above        |
| 2 | ⛔   | Invalid `pageSize`                 | 400    | `ValidationProblemDetails`   |

---

#### 🟡 `POST /admin/providers` — Create Provider (SC-11.2-02)

**Input (Body):**

| Field             | Type   | Required | Rule                              |
| ----------------- | ------ | -------- | --------------------------------- |
| name              | string | ✅       | 2 to 200 characters               |
| email             | string | ✅       | Valid email format                |
| phoneNumber       | string | ✅       | Valid mobile/phone format         |
| managerUsername   | string | ✅       | Unique system-wide, 3 to 100 characters |
| managerFullName   | string | ✅       | —                                 |
| managerPassword   | string | ✅       | Minimum 6 characters              |

**Successful output — 201 Created (+ `Location: /admin/providers/{providerId}` header):**

```json
{ "providerId": 35 }
```

| # | Type | Condition                                          | Status | Output                                                      |
| - | ---- | -------------------------------------------------- | ------ | ----------------------------------------------------------- |
| 1 | ✅   | All fields valid and `managerUsername` is unique   | 201    | `{ "providerId": <int> }`                                   |
| 2 | ⛔   | `managerUsername` already exists in the system     | 409    | `ProblemDetails` — "This username is already in use"        |
| 3 | ⛔   | Invalid `email` format                             | 400    | `ValidationProblemDetails` → `errors.email`                 |
| 4 | ⛔   | One or more required fields empty                  | 400    | `ValidationProblemDetails`                                  |

---

#### 🟠 `PATCH /admin/providers/{providerId}/deactivate` — Deactivate Provider (SC-11.2-03)

**Successful output:** `{ "success": true }`

| # | Type | Condition                        | Status | Output                                                              |
| - | ---- | -------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Valid `providerId`               | 200    | `{ "success": true }`; 💡 side effect: all subordinate users can no longer log in |
| 2 | ⛔   | `providerId` not found           | 404    | `ProblemDetails`                                                    |

### 2.3 Provider Detail

#### 🟢 `GET /admin/providers/{providerId}` — Get Detail (SC-11.3-01)

**Successful output (200):**

```json
{
  "name": "Nice Mind Tech Co.", "email": "info@nicemind.ir", "phone": "02112345678",
  "isActive": true,
  "subscription": { "planName": "Gold Plan", "expireDate": "2026-12-01T00:00:00Z" },
  "clientCount": 12, "agentCount": 5
}
```

| # | Type | Condition              | Status | Output           |
| - | ---- | ---------------------- | ------ | ---------------- |
| 1 | ✅   | Valid `providerId`     | 200    | Object above     |
| 2 | ⛔   | `providerId` not found | 404    | `ProblemDetails` |

---

#### 🟠 `PUT /admin/providers/{providerId}` — Update (SC-11.3-02)

**Input (Body):** `name, email, phoneNumber, isActive`

**Successful output:** `{ "success": true }`

| # | Type | Condition                | Status | Output                       |
| - | ---- | ------------------------ | ------ | ---------------------------- |
| 1 | ✅   | Valid values             | 200    | `{ "success": true }`        |
| 2 | ⛔   | `providerId` not found   | 404    | `ProblemDetails`             |
| 3 | ⛔   | Invalid `email` format   | 400    | `ValidationProblemDetails`   |

### 2.4 Plans Manager

#### 🔵 `GET /admin/plans` — Get Plans List (SC-11.4-01)

**Successful output — `PagedResponse<PlanListItem>`:**

```json
{
  "items": [
    { "id": 3, "name": "Gold Plan", "price": 12000000, "durationDays": 365, "maxClientCount": 50, "maxAgentCount": 20 }
  ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 5, "totalPages": 1
}
```

| # | Type | Condition | Status | Output                 |
| - | ---- | --------- | ------ | ---------------------- |
| 1 | ✅   | Always    | 200    | `PagedResponse` above  |

---

#### 🟡 `POST /admin/plans` — Create Plan (SC-11.4-02)

**Input (Body):**

| Field          | Type    | Required | Rule          |
| -------------- | ------- | -------- | ------------- |
| name           | string  | ✅       | —             |
| durationDays   | int     | ✅       | > 0           |
| price          | decimal | ✅       | ≥ 0           |
| maxClientCount | int     | ✅       | ≥ 1           |
| maxAgentCount  | int     | ✅       | ≥ 1           |
| modulesJson    | string  | ❌       | Valid JSON    |

**Successful output — 201:** `{ "planId": 6 }`

| # | Type | Condition                          | Status | Output                                              |
| - | ---- | ---------------------------------- | ------ | --------------------------------------------------- |
| 1 | ✅   | Valid values                       | 201    | `{ "planId": <int> }`                               |
| 2 | ⛔   | Negative `durationDays` or `price` | 400    | `ValidationProblemDetails`                          |
| 3 | ⛔   | Invalid `modulesJson` JSON         | 400    | `ValidationProblemDetails` → `errors.modulesJson` |

---

#### 🟠 `PUT /admin/plans/{planId}` — Update Plan (SC-11.4-03)

**Input (Body):** All `Plans` fields

**Successful output:** `{ "success": true }`

| # | Type | Condition              | Status | Output                                                              |
| - | ---- | ---------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Valid values           | 200    | `{ "success": true }` (no retroactive effect on current subscriptions) |
| 2 | ⛔   | `planId` not found     | 404    | `ProblemDetails`                                                    |
| 3 | ⛔   | Invalid numeric value  | 400    | `ValidationProblemDetails`                                          |

---

#### 🟠 `PATCH /admin/plans/{planId}/deactivate` — Deactivate Plan (SC-11.4-04)

**Successful output:** `{ "success": true }`

| # | Type | Condition                                        | Status | Output                                                              |
| - | ---- | ------------------------------------------------ | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Valid `planId`                                   | 200    | `{ "success": true }`; Providers already on this plan are not affected |
| 2 | ⛔   | `planId` not found                               | 404    | `ProblemDetails`                                                    |

### 2.5 Subscriptions Manager

#### 🔵 `GET /admin/providers/{providerId}/subscriptions` — Get Subscriptions List (SC-11.5-01)

**Input (Query):** `pageNumber, pageSize`

**Successful output — `PagedResponse<SubscriptionListItem>`:**

```json
{
  "items": [
    { "planName": "Gold Plan", "purchaseDate": "2025-12-01T00:00:00Z", "expireDate": "2026-12-01T00:00:00Z", "moneyPaid": 12000000, "isActive": true }
  ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 3, "totalPages": 1
}
```

| # | Type | Condition                | Status | Output                 |
| - | ---- | ------------------------ | ------ | ---------------------- |
| 1 | ✅   | Valid `providerId`       | 200    | `PagedResponse` above  |
| 2 | ⛔   | `providerId` not found   | 404    | `ProblemDetails`       |

---

#### 🟡 `POST /admin/providers/{providerId}/subscriptions` — Create/Renew Subscription (SC-11.5-02)

**Input (Body):** `planId: int, moneyPaid: decimal`

**Successful output — 201:**

```json
{ "subscriptionId": 88, "expireDate": "2027-07-07T00:00:00Z" }
```

| # | Type | Condition                                                 | Status | Output                                                    |
| - | ---- | --------------------------------------------------------- | ------ | --------------------------------------------------------- |
| 1 | ✅   | Valid `planId`; previous active subscription (if any) is deactivated | 201 | `{ "subscriptionId": <int>, "expireDate": <date> }` |
| 2 | ⛔   | `providerId`/`planId` not found                           | 404    | `ProblemDetails`                                          |
| 3 | ⛔   | Negative `moneyPaid`                                      | 400    | `ValidationProblemDetails`                                |

---

## 👔 3. ProviderManager Area (Section 12 of v6 doc)

> All Endpoints in this section are `[Authorize(Roles="ProviderManager")]` and an implicit `ProviderId == user's token` filter is applied to all queries.

### 3.1 Home Dashboard

#### 🟢 `GET /pm/dashboard/stats` — Get Dashboard Stats (SC-12.1-01)

**Successful output (200):**

```json
{
  "totalClients": 12, "totalAgents": 5,
  "openTicketsTotal": 34, "closedTicketsTotal": 210,
  "unassignedTicketsCount": 2
}
```

| # | Type | Condition                      | Status | Output           |
| - | ---- | ------------------------------ | ------ | ---------------- |
| 1 | ✅   | Always                         | 200    | Object above     |
| 2 | ⛔   | Role other than ProviderManager | 403   | `ProblemDetails` |

### 3.2 Agent Manager

#### 🔵 `GET /pm/agents` — Get Agents List (SC-12.2-01)

**Successful output — `PagedResponse<AgentListItem>`:**

```json
{
  "items": [
    { "id": 21, "fullName": "Reza Karimi", "username": "reza.k", "isActive": true, "openTicketsCount": 3, "closedTicketsCount": 40 }
  ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 5, "totalPages": 1
}
```

| # | Type | Condition                              | Status | Output                 |
| - | ---- | -------------------------------------- | ------ | ---------------------- |
| 1 | ✅   | Always (only own Provider's Agents)    | 200    | `PagedResponse` above  |

---

#### 🟡 `POST /pm/agents` — Create Agent (SC-12.2-02)

**Input (Body):** `fullName, username, phoneNumber, password`

**Successful output — 201:** `{ "userId": 22 }`

| # | Type | Condition                                                      | Status | Output                                                              |
| - | ---- | -------------------------------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Unique `username` and active Agent count < plan's `MaxAgentCount` | 201 | `{ "userId": <int> }`                                               |
| 2 | ⛔   | Duplicate `username`                                           | 409    | `ProblemDetails` — "This username is already in use"                |
| 3 | ⛔   | 💡 Current plan's `MaxAgentCount` limit reached                | 400    | `ProblemDetails` — "You have reached the maximum number of agents allowed by your plan" |
| 4 | ⛔   | Required field empty                                           | 400    | `ValidationProblemDetails`                                          |

---

#### 🟠 `PATCH /pm/agents/{userId}/deactivate` — Deactivate Agent (SC-12.2-03)

**Successful output:** `{ "success": true }`

| # | Type | Condition                              | Status | Output                                                              |
| - | ---- | -------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | `userId` belongs to own Provider       | 200    | `{ "success": true }`; 💡 their current open tickets are NOT auto-reassigned |
| 2 | ⛔   | `userId` belongs to another Provider   | 404    | `ProblemDetails`                                                    |

### 3.3 Agent Detail

#### 🟢 `GET /pm/agents/{userId}` — Get Detail (SC-12.3-01)

**Successful output (200):**

```json
{ "fullName": "Reza Karimi", "username": "reza.k", "phoneNumber": "09123334444", "isActive": true, "stats": { "openCount": 3, "closedCount": 40 } }
```

| # | Type | Condition                                  | Status | Output           |
| - | ---- | ------------------------------------------ | ------ | ---------------- |
| 1 | ✅   | Valid `userId` and same Provider           | 200    | Object above     |
| 2 | ⛔   | Invalid `userId` or from another Provider  | 404    | `ProblemDetails` |

---

#### 🟠 `PUT /pm/agents/{userId}` — Update (SC-12.3-02)

**Input (Body):** `fullName, phoneNumber, isActive`

**Successful output:** `{ "success": true }`

| # | Type | Condition                      | Status | Output                       |
| - | ---- | ------------------------------ | ------ | ---------------------------- |
| 1 | ✅   | Valid values                   | 200    | `{ "success": true }`        |
| 2 | ⛔   | Invalid `userId`               | 404    | `ProblemDetails`             |
| 3 | ⛔   | Invalid `phoneNumber` format   | 400    | `ValidationProblemDetails`   |

---

#### 🟢 `GET /pm/agents/{userId}/stats` — Get Ticket Stats (SC-12.3-03)

**Successful output (200):** `{ "openCount": 3, "closedCount": 40, "avgResponseTime": "02:15:00" }`

| # | Type | Condition          | Status | Output           |
| - | ---- | ------------------ | ------ | ---------------- |
| 1 | ✅   | Valid `userId`     | 200    | Object above     |
| 2 | ⛔   | Invalid `userId`   | 404    | `ProblemDetails` |

### 3.4 Client Manager

#### 🔵 `GET /pm/clients` — Get Clients List (SC-12.4-01)

**Successful output — `PagedResponse<ClientListItem>`:**

```json
{
  "items": [ { "id": 7, "name": "Alpha Corp", "email": "info@alpha.com", "isActive": true, "openTicketsCount": 4 } ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 12, "totalPages": 1
}
```

| # | Type | Condition | Status | Output                 |
| - | ---- | --------- | ------ | ---------------------- |
| 1 | ✅   | Always    | 200    | `PagedResponse` above  |

---

#### 🟡 `POST /pm/clients` — Create Client (SC-12.4-02)

**Input (Body):** `name, email, phoneNumber, managerUsername, managerFullName, managerPassword`

**Successful output — 201:** `{ "clientId": 8 }`

| # | Type | Condition                                                      | Status | Output                                                              |
| - | ---- | -------------------------------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Unique `managerUsername` and active Client count < plan's `MaxClientCount` | 201 | `{ "clientId": <int> }`                                     |
| 2 | ⛔   | Duplicate `managerUsername`                                    | 409    | `ProblemDetails`                                                    |
| 3 | ⛔   | 💡 Current plan's `MaxClientCount` limit reached               | 400    | `ProblemDetails` — "You have reached the maximum number of clients allowed by your plan" |
| 4 | ⛔   | Required field empty/invalid `email`                           | 400    | `ValidationProblemDetails`                                          |

---

#### 🟠 `PATCH /pm/clients/{clientId}/deactivate` — Deactivate Client (SC-12.4-03)

**Successful output:** `{ "success": true }`

| # | Type | Condition                              | Status | Output                                                              |
| - | ---- | -------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | `clientId` belongs to own Provider     | 200    | `{ "success": true }`; 💡 ClientManager and all Requesters of this Client can no longer log in |
| 2 | ⛔   | `clientId` belongs to another Provider | 404    | `ProblemDetails`                                                    |

### 3.5 Client Detail

#### 🟢 `GET /pm/clients/{clientId}` — Get Detail (SC-12.5-01)

**Successful output (200):**

```json
{ "name": "Alpha Corp", "email": "info@alpha.com", "phone": "02177889900", "isActive": true, "requesterCount": 8, "ticketStats": { "open": 4, "closed": 30 } }
```

| # | Type | Condition            | Status | Output           |
| - | ---- | -------------------- | ------ | ---------------- |
| 1 | ✅   | Valid `clientId`     | 200    | Object above     |
| 2 | ⛔   | Invalid `clientId`   | 404    | `ProblemDetails` |

---

#### 🟠 `PUT /pm/clients/{clientId}` — Update (SC-12.5-02)

**Input (Body):** `name, email, phoneNumber, isActive`

**Successful output:** `{ "success": true }`

| # | Type | Condition            | Status | Output                       |
| - | ---- | -------------------- | ------ | ---------------------------- |
| 1 | ✅   | Valid values         | 200    | `{ "success": true }`        |
| 2 | ⛔   | Invalid `clientId`   | 404    | `ProblemDetails`             |
| 3 | ⛔   | Invalid `email`      | 400    | `ValidationProblemDetails`   |

---

## 🎧 4. Agent Area (Section 13 of v6 doc)

> All Endpoints are `[Authorize(Roles="Agent")]`.

### 4.1 Ticket List

#### 🔵 `GET /agent/tickets` — Get Tickets List (SC-13.1-01)

**Input (Query):** `search?, status?: TicketStatus, priority?: TicketPriority, assignedToMe?: bool, pageNumber, pageSize`

**Successful output — `PagedResponse<TicketListItem>`:**

```json
{
  "items": [
    {
      "id": 55, "topic": "Login panel error", "clientName": "Alpha Corp", "requesterName": "Sara Ahmadi",
      "status": "InProgress", "priority": "High", "openDate": "2026-07-05T08:00:00Z", "isSeen": true
    }
  ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 34, "totalPages": 2
}
```

| # | Type | Condition                                       | Status | Output                                                       |
| - | ---- | ----------------------------------------------- | ------ | ------------------------------------------------------------ |
| 1 | ✅   | No filter — all Provider tickets                | 200    | `PagedResponse` above                                        |
| 2 | ✅   | With `assignedToMe=true`                        | 200    | Only tickets where `AssignedAgentId==self`                   |
| 3 | ✅   | With `status`/`priority` outside Enum values    | 400    | `ValidationProblemDetails` → `errors.status`                 |

### 4.2 Ticket Chat

#### 🟢 `GET /agent/tickets/{ticketId}` — Get Ticket Detail (SC-13.2-01)

**Successful output — `TicketDetail` (200):**

```json
{
  "topic": "Login panel error", "status": "InProgress", "priority": "High",
  "clientName": "Alpha Corp", "requesterName": "Sara Ahmadi", "assignedAgentName": "Reza Karimi"
}
```

| # | Type | Condition                                  | Status | Output                  |
| - | ---- | ------------------------------------------ | ------ | ----------------------- |
| 1 | ✅   | Ticket belongs to Agent's Provider         | 200    | `TicketDetail` above    |
| 2 | ⛔   | Ticket from another Provider or not found  | 404    | `ProblemDetails`        |

---

#### 🔵 `GET /agent/tickets/{ticketId}/messages` — Get Messages History (SC-13.2-02)

**Input (Query):** `pageNumber, pageSize`

**Successful output — `PagedResponse<TicketMessage>`:**

```json
{
  "items": [
    {
      "senderId": 40, "senderName": "Sara Ahmadi", "text": "The panel won't load",
      "createdAt": "2026-07-05T08:00:00Z", "seenAt": "2026-07-05T08:10:00Z",
      "attachments": [ { "fileUrl": "https://cdn.../a.png", "fileName": "screenshot.png", "fileType": "image/png", "fileSizeKB": 220 } ]
    }
  ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 12, "totalPages": 1
}
```

| # | Type | Condition                      | Status | Output                 |
| - | ---- | ------------------------------ | ------ | ---------------------- |
| 1 | ✅   | Ticket is same Provider        | 200    | `PagedResponse` above  |
| 2 | ⛔   | Ticket from another Provider   | 404    | `ProblemDetails`       |

---

#### 🟡 `POST /agent/tickets/{ticketId}/messages` — Send Message (SC-13.2-03)

**Prerequisite:** `AssignedAgentId == current Agent`

**Input (Body):** `text: string (required, non-empty), attachments?: file[]`

**Successful output — 201:** `{ "messageId": 301 }`

| # | Type | Condition                                              | Status | Output                                                              |
| - | ---- | ------------------------------------------------------ | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Agent is the current ticket owner                      | 201    | `{ "messageId": <int> }`; 🔔 `NewMessage` Notification for Requester |
| 2 | ⛔   | Another Agent is responsible (Reassigned)              | 403    | `ProblemDetails` — "You are no longer responsible for this ticket"  |
| 3 | ⛔   | Ticket from another Provider                           | 404    | `ProblemDetails`                                                    |
| 4 | ⛔   | Empty `text` and no `attachments`                      | 400    | `ValidationProblemDetails` — "Message text or attachment is required" |

---

#### 🟠 `PATCH /agent/tickets/{ticketId}/seen` — Mark as Seen (SC-13.2-04)

**Successful output:** `{ "success": true }`

| # | Type | Condition                      | Status | Output                                       |
| - | ---- | ------------------------------ | ------ | -------------------------------------------- |
| 1 | ✅   | Ticket is same Provider        | 200    | `{ "success": true }` (sets `SeenByAgentDate`) |
| 2 | ⛔   | Ticket from another Provider   | 404    | `ProblemDetails`                             |

---

#### 🟠 `PATCH /agent/tickets/{ticketId}/reassign` — Reassign Ticket (SC-13.2-05 / ALG-2)

**Input (Body):** `{ "newAgentId": 30 }`

**Successful output:** `{ "success": true }`

| # | Type | Condition                                                      | Status | Output                                                        |
| - | ---- | -------------------------------------------------------------- | ------ | ------------------------------------------------------------- |
| 1 | ✅   | `AssignedAgentId == self` and `newAgentId` is active same Provider | 200 | `{ "success": true }`; 🔔 `TicketReassigned` for new Agent |
| 2 | ⛔   | Agent is not the ticket owner                                  | 403    | `ProblemDetails` — "You do not have permission to reassign this ticket" |
| 3 | ⛔   | `newAgentId` inactive/deleted/from another Provider            | 400    | `ProblemDetails` — "Selected agent is not valid"              |
| 4 | ⛔   | `newAgentId` does not exist                                    | 404    | `ProblemDetails`                                              |

---

#### 🟠 `PATCH /agent/tickets/{ticketId}/status` — Update Status (SC-13.2-06)

**Input (Body):** `{ "newStatus": "Resolved" }` (value from `TicketStatus` Enum)

**Successful output:** `{ "success": true }`

| # | Type | Condition                                       | Status | Output                                                              |
| - | ---- | ----------------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Agent is ticket owner, valid Enum value         | 200    | `{ "success": true }`                                               |
| 2 | ✅   | New value `Resolved`                            | 200    | Same as above + 🔔 `TicketResolved` for Requester + `CloseDate` recorded |
| 3 | ✅   | New value `Closed`                              | 200    | Same as above + 🔔 `TicketClosed` for Requester + `CloseDate` recorded |
| 4 | ⛔   | Agent is not ticket owner                       | 403    | `ProblemDetails`                                                    |
| 5 | ⛔   | `newStatus` outside Enum values                 | 400    | `ValidationProblemDetails` → `errors.newStatus`                     |

---

#### 🟠 `PATCH /agent/tickets/{ticketId}/priority` — Update Priority (SC-13.2-07)

**Input (Body):** `{ "priority": "High" }`

**Successful output:** `{ "success": true }`

| # | Type | Condition                      | Status | Output                       |
| - | ---- | ------------------------------ | ------ | ---------------------------- |
| 1 | ✅   | Valid Enum value               | 200    | `{ "success": true }`        |
| 2 | ⛔   | Value outside Enum             | 400    | `ValidationProblemDetails`   |
| 3 | ⛔   | Ticket from another Provider   | 404    | `ProblemDetails`             |

---

#### 🔵 `GET /agent/tickets/{ticketId}/notes` — Get Internal Notes (SC-13.2-08)

**Successful output — `TicketNote[]` (200):**

```json
[ { "authorName": "Reza Karimi", "text": "Waiting for response from the technical team", "createdAt": "2026-07-06T10:00:00Z" } ]
```

| # | Type | Condition                      | Status | Output                              |
| - | ---- | ------------------------------ | ------ | ----------------------------------- |
| 1 | ✅   | Ticket is same Provider        | 200    | Array above (may be empty)          |
| 2 | ⛔   | Ticket from another Provider   | 404    | `ProblemDetails`                    |

---

#### 🟡 `POST /agent/tickets/{ticketId}/notes` — Add Internal Note (SC-13.2-09)

**Input (Body):** `{ "text": "string" }`

**Successful output — 201:** `{ "noteId": 12 }`

| # | Type | Condition                      | Status | Output                       |
| - | ---- | ------------------------------ | ------ | ---------------------------- |
| 1 | ✅   | Non-empty `text`               | 201    | `{ "noteId": <int> }`        |
| 2 | ⛔   | Empty `text`                   | 400    | `ValidationProblemDetails`   |
| 3 | ⛔   | Ticket from another Provider   | 404    | `ProblemDetails`             |

---

#### 🔵 `GET /agent/active-agents` — Get Active Agents for Reassign (SC-13.2-10)

**Successful output (200):**

```json
[ { "id": 30, "fullName": "Sara Rostami", "openTicketsCount": 1 } ]
```

| # | Type | Condition | Status | Output                                                              |
| - | ---- | --------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Always    | 200    | Array of active same-Provider Agents (excluding requester optional) |

---

## 🏢 5. ClientManager Area (Section 14 of v6 doc)

> All Endpoints are `[Authorize(Roles="ClientManager")]` and there are **no** write Endpoints on tickets.

### 5.1 Dashboard

#### 🟢 `GET /cm/dashboard/stats` — Get Dashboard Stats (SC-14.1-01)

**Successful output (200):** `{ "totalRequesters": 8, "openTicketsTotal": 4, "closedTicketsTotal": 30 }`

| # | Type | Condition | Status | Output           |
| - | ---- | --------- | ------ | ---------------- |
| 1 | ✅   | Always    | 200    | Object above     |

### 5.2 Requester Manager

#### 🔵 `GET /cm/requesters` — Get Requesters List (SC-14.2-01)

**Successful output — `PagedResponse<RequesterListItem>`:**

```json
{ "items": [ { "id": 40, "fullName": "Sara Ahmadi", "username": "sara.a", "isActive": true, "ticketsCreatedCount": 6 } ], "pageNumber": 1, "pageSize": 20, "totalCount": 8, "totalPages": 1 }
```

| # | Type | Condition | Status | Output                 |
| - | ---- | --------- | ------ | ---------------------- |
| 1 | ✅   | Always    | 200    | `PagedResponse` above  |

---

#### 🟡 `POST /cm/requesters` — Create Requester (SC-14.2-02)

**Input (Body):** `fullName, username, phoneNumber, password`

**Successful output — 201:** `{ "userId": 41 }`

| # | Type | Condition              | Status | Output                       |
| - | ---- | ---------------------- | ------ | ---------------------------- |
| 1 | ✅   | Unique `username`      | 201    | `{ "userId": <int> }`        |
| 2 | ⛔   | Duplicate `username`   | 409    | `ProblemDetails`             |
| 3 | ⛔   | Required field empty   | 400    | `ValidationProblemDetails`   |

---

#### 🟠 `PATCH /cm/requesters/{userId}/deactivate` — Deactivate Requester (SC-14.2-03)

**Successful output:** `{ "success": true }`

| # | Type | Condition                          | Status | Output                                                              |
| - | ---- | ---------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | `userId` belongs to own Client     | 200    | `{ "success": true }` (their previous tickets remain untouched)     |
| 2 | ⛔   | `userId` belongs to another Client | 404    | `ProblemDetails`                                                    |

### 5.3 Requester Detail

#### 🟢 `GET /cm/requesters/{userId}` — Get Detail (SC-14.3-01)

**Successful output (200):** `{ "fullName": "Sara Ahmadi", "username": "sara.a", "phoneNumber": "09131112222", "isActive": true, "ticketStats": { "open": 2, "closed": 4 } }`

| # | Type | Condition          | Status | Output           |
| - | ---- | ------------------ | ------ | ---------------- |
| 1 | ✅   | Valid `userId`     | 200    | Object above     |
| 2 | ⛔   | Invalid `userId`   | 404    | `ProblemDetails` |

---

#### 🟠 `PUT /cm/requesters/{userId}` — Update (SC-14.3-02)

**Input (Body):** `fullName, phoneNumber, isActive`

**Successful output:** `{ "success": true }`

| # | Type | Condition          | Status | Output                       |
| - | ---- | ------------------ | ------ | ---------------------------- |
| 1 | ✅   | Valid values       | 200    | `{ "success": true }`        |
| 2 | ⛔   | Invalid `userId`   | 404    | `ProblemDetails`             |

### 5.4 Ticket List (Read-Only)

#### 🔵 `GET /cm/tickets` — Get All Client Tickets (SC-14.4-01)

**Input (Query):** `search?, status?, pageNumber, pageSize`

**Successful output — `PagedResponse<ClientTicketListItem>`:**

```json
{
  "items": [ { "id": 55, "topic": "Login panel error", "requesterName": "Sara Ahmadi", "assignedAgentName": "Reza Karimi", "status": "InProgress", "priority": "High" } ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 34, "totalPages": 2
}
```

| # | Type | Condition                                                              | Status | Output                       |
| - | ---- | ---------------------------------------------------------------------- | ------ | ---------------------------- |
| 1 | ✅   | Always (metadata only, no message text)                                | 200    | `PagedResponse` above        |
| 2 | ⛔   | Attempt to call chat/status change Endpoints (not available for this role) | 403/404 (Route not authorized for role) | `ProblemDetails` |

---

## 👤 6. Requester Area (Section 15 of v6 doc)

> All Endpoints are `[Authorize(Roles="Requester")]`.

### 6.1 New Ticket

#### 🟡 `POST /requester/tickets` — Create Ticket (SC-15.1-01)

**Input (Body):**

| Field       | Type             | Required | Rule                           |
| ----------- | ---------------- | -------- | ------------------------------ |
| topic       | string           | ✅       | 3 to 300 characters            |
| text        | string           | ✅       | Non-empty                      |
| priority    | `TicketPriority` | ❌       | Default `Medium`               |
| attachments | file[]           | ❌       | —                              |

**Successful output — 201:** `{ "ticketId": 56 }`

| # | Type | Condition                                                      | Status | Output                                                                              |
| - | ---- | -------------------------------------------------------------- | ------ | ----------------------------------------------------------------------------------- |
| 1 | ✅   | Valid `topic`/`text` + active Agent exists in Provider         | 201    | `{ "ticketId": <int> }`; 🔔 `NewTicketAssigned` for selected Agent (per ALG-1)      |
| 2 | ✅   | Valid `topic`/`text` but **no active Agent exists**            | 201    | `{ "ticketId": <int> }`; ticket created with `assignedAgentName: null` (Unassigned) |
| 3 | ⛔   | Empty `topic` or `text`                                        | 400    | `ValidationProblemDetails` → `errors.topic` / `errors.text`                         |
| 4 | ⛔   | `priority` outside Enum                                        | 400    | `ValidationProblemDetails` → `errors.priority`                                      |

### 6.2 Ticket List

#### 🔵 `GET /requester/tickets` — Get Client Tickets (SC-15.2-01)

**Input (Query):** `search?, status?, createdByMe?: bool, pageNumber, pageSize`

**Successful output — `PagedResponse<RequesterTicketListItem>`:**

```json
{
  "items": [ { "id": 56, "topic": "Login panel error", "requesterName": "Sara Ahmadi", "status": "Open", "priority": "Medium", "openDate": "2026-07-07T09:00:00Z" } ],
  "pageNumber": 1, "pageSize": 20, "totalCount": 6, "totalPages": 1
}
```

| # | Type | Condition                                          | Status | Output                                              |
| - | ---- | -------------------------------------------------- | ------ | --------------------------------------------------- |
| 1 | ✅   | No filter — all Client tickets (including colleagues') | 200 | `PagedResponse` above                               |
| 2 | ✅   | With `createdByMe=true`                            | 200    | Only tickets where `RequesterId==self`              |

### 6.3 Ticket Chat

#### 🟢 `GET /requester/tickets/{ticketId}` — Get Ticket Detail (SC-15.3-01)

**Successful output (200):** `{ "topic": "...", "status": "Open", "priority": "Medium", "assignedAgentName": "Reza Karimi", "requesterName": "Sara Ahmadi" }`

| # | Type | Condition                                                      | Status | Output           |
| - | ---- | -------------------------------------------------------------- | ------ | ---------------- |
| 1 | ✅   | Ticket `ClientId == self` (even if not the original creator)   | 200    | Object above     |
| 2 | ⛔   | Ticket from another Client                                     | 404    | `ProblemDetails` |

---

#### 🔵 `GET /requester/tickets/{ticketId}/messages` — Get Messages History (SC-15.3-02)

**Successful output:** Same structure as `SC-13.2-02`

| # | Type | Condition                  | Status | Output                       |
| - | ---- | -------------------------- | ------ | ---------------------------- |
| 1 | ✅   | Ticket `ClientId == self`  | 200    | `PagedResponse<TicketMessage>` |
| 2 | ⛔   | Ticket from another Client | 404    | `ProblemDetails`             |

---

#### 🟡 `POST /requester/tickets/{ticketId}/messages` — Send Message (SC-15.3-03)

**Prerequisite:** `RequesterId == current Requester` (original creator only)

**Input (Body):** `text: string, attachments?: file[]`

**Successful output — 201:** `{ "messageId": 302 }`

| # | Type | Condition                                          | Status | Output                                                              |
| - | ---- | -------------------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Sender is the original ticket creator              | 201    | `{ "messageId": <int> }`; 🔔 `NewMessage` for current assigned Agent |
| 2 | ⛔   | Sender is not the original creator (colleague Requester) | 403 | `ProblemDetails` — "Only the ticket creator can send messages" |
| 3 | ⛔   | Ticket from another Client                         | 404    | `ProblemDetails`                                                    |
| 4 | ⛔   | Empty `text` and no attachments                    | 400    | `ValidationProblemDetails`                                          |

---

#### 🟠 `PATCH /requester/tickets/{ticketId}/reopen` — Reopen Ticket (SC-15.3-04 / ALG-4)

**Prerequisite:** `RequesterId == self` and `Status IN [Resolved, Closed]`

**Successful output:** `{ "success": true }`

| # | Type | Condition                                          | Status | Output                                                              |
| - | ---- | -------------------------------------------------- | ------ | ------------------------------------------------------------------- |
| 1 | ✅   | Original creator + current status `Resolved`/`Closed` | 200 | `{ "success": true }`; same Agent remains; 🔔 `TicketReopened`      |
| 2 | ⛔   | Sender is not the original creator                 | 403    | `ProblemDetails`                                                    |
| 3 | ⛔   | Current status is not `Resolved`/`Closed`            | 409    | `ProblemDetails` — "Only resolved/closed tickets can be reopened" |
| 4 | ⛔   | Ticket from another Client                         | 404    | `ProblemDetails`                                                    |

---

## 🗺️ 7. Endpoint ↔ Role Mapping Summary

| Route Prefix | Authorized Role   | Endpoint Count |
| ------------ | ----------------- | -------------- |
| `/auth/*`    | Public            | 5              |
| `/profile/*`, `/layout/*`, `/notifications/*` | All roles | 9 |
| `/admin/*`   | SuperAdmin        | 12             |
| `/pm/*`      | ProviderManager   | 12             |
| `/agent/*`   | Agent             | 11             |
| `/cm/*`      | ClientManager     | 7              |
| `/requester/*` | Requester       | 6              |
| **Total**    | —                 | **62**         |

> 📌 For step-by-step frontend implementation based on this table, refer to `TICKETING-SYSTEM-IMPLEMENTATION-PHASES-v1.md`.
