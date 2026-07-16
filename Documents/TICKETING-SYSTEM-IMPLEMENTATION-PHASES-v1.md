# 🚀 Ticketing System — Frontend Implementation Phasing & Guide v1

> **Audience:** Frontend Team
> **Backend:** ASP.NET Core (.NET 10) — JWT Bearer Auth
> **Required reading before this document:**
> 1. `TICKETING-SYSTEM-DESIGN-v6.md` — Business logic and database
> 2. `TICKETING-SYSTEM-API-SCENARIOS-v2.md` — Exact contract for each Endpoint (input/output/status code)
> 3. `openapi.json` — The same contract in machine-readable form, importable into Swagger UI / Postman / Codegen tools
> 4. `TICKETING-SYSTEM-BACKEND-IMPLEMENTATION-GUIDE-v1.md` — Backend build order (same phase numbers 0–6); use it so FE/BE stay aligned

This document tells you **in what order** and **why in that order** you should implement the pages, what logic sits behind each API, and what must work at the end of each phase.

> ⚠️ **FE/BE sync:** Do not start UI for Phase N until backend Phase N DoD in the Backend Implementation Guide is marked ready (Swagger demo of those endpoints).

---

## 📖 Table of Contents

| Phase | Title                                            | Depends On        |
| --- | -------------------------------------------------- | ----------------- |
| 0   | Shared Infrastructure (Auth, Interceptor, Base Layout)    | —                  |
| 1   | SuperAdmin Area                                  | Phase 0              |
| 2   | ProviderManager Area                             | Phase 1 (a Provider must already exist) |
| 3   | Requester Area (Create and View Tickets)            | Phase 2 (a Client/Requester must already exist) |
| 4   | Agent Area (Ticket Handling)                      | Phases 2 and 3 (requires an Agent and at least one real ticket) |
| 5   | ClientManager Area (Read-Only Oversight)                  | Phase 3              |
| 6   | Final Integration (Polling, Notifications, Full Testing)   | All previous phases     |

---

## 🧰 0. Infrastructure Checklist — Must Be Complete Before Any Phase

### 0.1 Using `openapi.json`

The strongest way to start: open this file with one of the tools below to avoid manually typing models:

- **Swagger UI / Swagger Editor** (`https://editor.swagger.io` → Import File) for visual browsing and manual testing of all Endpoints
- **NSwag** or **openapi-generator-cli** or **orval** to auto-generate TypeScript Client + Types directly from `openapi.json` (strongly recommended — saves you from writing TypeScript interfaces by hand)
- **Postman** → Import → the same file, for quick manual testing of each Endpoint before wiring the UI

### 0.2 Shared HTTP Client Layer (Build Once, Use Everywhere)

Before starting Phase 1, implement the following in one place (e.g. `apiClient`) so that in each phase you only write page logic:

1. **Base URL** read from an Environment Variable (`/api/v1`).
2. **Request Interceptor:** automatically add the `Authorization: Bearer {accessToken}` header (read from secure Storage).
3. **Error Response Interceptor:**
   - `401` → attempt once to call `POST /auth/refresh-token`; if that also returns `401` → redirect the user to the Login page and clear Storage.
   - `400` → extract the `errors` object and pass it to the form so it displays under each Input (structure of `ValidationProblemDetails`, section 0.4 of the scenarios document v2).
   - `403` / `404` / `409` → use the `detail` field to show a Toast/error message.
   - `500` → generic message: "An unexpected error occurred. Please try again."
4. **Pagination Helper:** a shared Hook/Composable/Service for managing `pageNumber`, `pageSize`, `search` and parsing `PagedResponse` (section 0.3 of the scenarios document v2) — all list pages should use this.
5. **Role-Based Route Guard:** after Login, store `roleName` in Storage and make each area's routes (`/admin/*`, `/pm/*`, `/agent/*`, `/cm/*`, `/requester/*`) accessible only to the matching role (redirect otherwise).
6. **File Upload (Attachments):** `multipart/form-data` requests (`Send Message`, `Create Ticket`) use the same apiClient but with a separate `Content-Type: multipart/form-data`.

### 0.3 Phase 0 — Shared Pages (Real Prerequisite for All Roles)

| Page           | Related Endpoint(s)                                                             | Behind-the-Scenes Logic                                                                                  |
| -------------- | --------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Login Page      | `POST /auth/login`                                                                 | Server validates username/password and checks `IsActive`/`IsDeleted`; the frontend only stores the token |
| Forgot Password    | `POST /auth/forgot-password`, `POST /auth/reset-password`                          | Always return success to prevent revealing whether a user exists (explained in the scenarios document, SC-10.2-01)                 |
| Main Layout    | `GET /layout/profile-summary`, `GET /layout/notifications/unread-count`            | Show user name/role and notification count badge in the header; poll `unread-count` every 15–30 seconds              |
| My Profile     | `GET /profile`, `PUT /profile`, `PUT /profile/change-password`, `GET /profile/subscription` (ProviderManager only) | —                                                                                                   |
| Notifications       | `GET /notifications`, `PATCH /notifications/{id}/read`, `PATCH /notifications/read-all` | Poll the list every 15–30 seconds or when the notification Dropdown opens                                        |

**✅ Phase 0 Definition of Done:**

- Users with different roles can log in and the Session persists after a page Refresh (test Refresh Token).
- A 401 error automatically clears the Session and redirects to Login.
- A 400 error displays under the correct form field (test with a simple form like Change Password).
- The unread notification count badge in the header works.

---

## 🛡️ 1. Phase 1 — SuperAdmin Area

**Why this phase first?** Because without at least one `Provider` with an active subscription, no `ProviderManager` can log in and the entire chain (Provider → Client → Requester → Ticket → Agent) cannot be formed. This phase creates the base data (like a Seed) for the remaining phases.

| Page                  | Endpoint(s)                                                                      | Behind-the-Scenes Logic You Must Know                                                                                     |
| ----------------------- | ------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------- |
| Overall Dashboard           | `GET /admin/dashboard/stats`                                                        | Aggregated statistics; no special filters                                                                                       |
| Provider Management      | `GET /admin/providers`, `POST /admin/providers`, `PATCH /admin/providers/{id}/deactivate` | Creating a Provider also creates a `ProviderManager` user at the same time (fields `managerUsername/managerFullName/managerPassword`) |
| Provider Details        | `GET /admin/providers/{id}`, `PUT /admin/providers/{id}`                            | —                                                                                                                   |
| Plan Management          | `GET /admin/plans`, `POST /admin/plans`, `PUT /admin/plans/{id}`, `PATCH /admin/plans/{id}/deactivate` | `modulesJson` is a free-form JSON string — for now you can use a simple Textarea                              |
| Subscription Management       | `GET /admin/providers/{id}/subscriptions`, `POST /admin/providers/{id}/subscriptions` | Must be done **after** creating at least one Provider and one Plan; without an active subscription, `ProviderManager` in Phase 2 will hit issues (`SC-10.3-04` returns 404) |

**⚠️ Important note for frontend testing:** To test Phase 2 and beyond, you must manually complete the full path: **Create Plan → Create Provider → Create Subscription for that Provider**. Try this order in Postman/Swagger UI before writing code.

**✅ Phase 1 Definition of Done:**

- You can create a new Provider and immediately log in with the created `managerUsername` via Phase 0 (Login page).
- Duplicate `managerUsername` error (409) displays correctly under the form.
- After purchasing a subscription, `GET /profile/subscription` (used in Phase 2) no longer returns 404.

---

## 👔 2. Phase 2 — ProviderManager Area

**Why this phase after SuperAdmin?** Because access to this area requires a valid Provider with an active subscription (output of Phase 1). Also, before the Agent/Requester phases, at least one Agent and one Client (with a ClientManager) must be created.

| Page              | Endpoint(s)                                                                                | Behind-the-Scenes Logic                                                                                                    |
| ------------------- | ---------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Dashboard          | `GET /pm/dashboard/stats`                                                                     | `unassignedTicketsCount` is important — it means tickets that no active Agent could be assigned to (ALG-5 in scenarios document v1)  |
| Agent Management    | `GET /pm/agents`, `POST /pm/agents`, `PATCH /pm/agents/{id}/deactivate`                        | ⚠️ Respect the current Provider plan's `MaxAgentCount` limit; if full, the server returns 400 — display the message directly |
| Agent Details       | `GET /pm/agents/{id}`, `PUT /pm/agents/{id}`, `GET /pm/agents/{id}/stats`                      | —                                                                                                                   |
| Client Management   | `GET /pm/clients`, `POST /pm/clients`, `PATCH /pm/clients/{id}/deactivate`                     | Like creating a Provider, creating a Client also creates a `ClientManager` user at the same time; respect the plan's `MaxClientCount` limit   |
| Client Details      | `GET /pm/clients/{id}`, `PUT /pm/clients/{id}`                                                 | —                                                                                                                   |

**⚠️ Key note for End-to-End testing:** After this phase, be sure to create **at least one active Agent**. If you skip this, in Phase 3 when a Requester creates a ticket, `AssignedAgentId` will always remain `null` and you cannot fully test Phase 4 (Agent).

**✅ Phase 2 Definition of Done:**

- At least one active Agent and one Client (with a Requester, created in Phase 3) exist.
- Plan limit errors (`MaxAgentCount`/`MaxClientCount`) are shown to the user, not only in the browser Console.

---

## 👤 3. Phase 3 — Requester Area (Create and View Tickets)

**Why before Agent?** Because a ticket must exist so Agent pages (Phase 4) have real data to test with. From a business logic standpoint, this phase is simpler than the Agent phase (only the original creator sends messages), so it finishes sooner.

| Page                | Endpoint(s)                                                              | Behind-the-Scenes Logic You Must Enforce in the UI                                                                        |
| --------------------- | ----------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Create New Ticket       | `POST /requester/tickets` (multipart/form-data)                              | After submission, the server automatically runs the assignment algorithm (ALG-1) — the frontend only receives `ticketId` and redirects to that ticket's chat page |
| My Tickets List     | `GET /requester/tickets` with `createdByMe=true`                               | "My Tickets" tab vs. "All Organization Tickets" tab (no filter) — per section 15.2 of document v6, colleagues have Read-only access       |
| Ticket Chat              | `GET /requester/tickets/{id}`, `GET /requester/tickets/{id}/messages`, `POST /requester/tickets/{id}/messages` | ⚠️ **If the current user is not the original ticket creator** (i.e. `requesterName` in ticket details does not match the current user's name), the message send box must be **disabled/hidden** and shown Read-only only — the server also returns 403 but it's better to prevent it in the UI |
| Reopen Ticket        | `PATCH /requester/tickets/{id}/reopen`                                        | Only show the "Reopen" button when `status` is `Resolved` or `Closed`; after reopening, the same previous Agent remains responsible |

**✅ Phase 3 Definition of Done:**

- A new ticket is created successfully and its first message appears in chat.
- Another Requester colleague (who is not the original creator) can view the ticket but cannot send messages (button disabled).
- The Reopen button appears only on `Resolved`/`Closed` tickets.

---

## 🎧 4. Phase 4 — Agent Area (Most Complex Phase in Terms of Logic)

**Why last (among operational areas)?** Because all sensitive system logic (Auto-Assignment, Reassign, status changes, internal notes) is concentrated here, and real testing requires real tickets from Phase 3.

| Page                    | Endpoint(s)                                                                                                          | Behind-the-Scenes Logic You Must Know                                                                                                          |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Ticket List             | `GET /agent/tickets` (filters `assignedToMe`, `status`, `priority`)                                                          | Use the `isSeen` column to show a "new" dot/bold row                                                                                 |
| Ticket Chat                 | `GET /agent/tickets/{id}`, `GET /agent/tickets/{id}/messages`, `POST /agent/tickets/{id}/messages`, `PATCH /agent/tickets/{id}/seen` | When the chat page opens, immediately call `PATCH .../seen`; ⚠️ if the ticket was Reassigned and another person is responsible, the send message button must be disabled (403 from server) |
| Reassign                | `GET /agent/active-agents`, `PATCH /agent/tickets/{id}/reassign`                                                            | Only show the Reassign button on tickets where `AssignedAgentId == current Agent`; fetch the active Agent list from the first Endpoint and show it to the user (open ticket count for each alongside) |
| Status/Priority Change      | `PATCH /agent/tickets/{id}/status`, `PATCH /agent/tickets/{id}/priority`                                                    | Get Enum values from a fixed Dropdown, not Free Text; show a Confirm Dialog on `Resolved`/`Closed` because this action cannot be undone (only via Reopen by Requester) |
| Internal Note           | `GET /agent/tickets/{id}/notes`, `POST /agent/tickets/{id}/notes`                                                           | ⚠️ Keep this section in a completely separate tab/section from the main chat — these must never appear in the same Thread as Requester messages                       |

**✅ Phase 4 Definition of Done:**

- You successfully Reassign a ticket from Agent A to Agent B and see that Agent A can no longer send messages in it.
- Changing status to `Resolved` activates the Reopen button on the Requester side (Phase 3) — this is an End-to-End test across two roles; be sure to run it.
- A recorded internal note never appears in the Requester-side chat (verify both sides).

---

## 🏢 5. Phase 5 — ClientManager Area (Simplest Phase, Saved for Last)

**Why last?** Because this area is entirely oversight with no write logic on tickets; it does not need complex data and can be implemented last with minimal risk.

| Page                     | Endpoint(s)                                                                                             | Behind-the-Scenes Logic                                                                                    |
| -------------------------- | ------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------- |
| Dashboard                 | `GET /cm/dashboard/stats`                                                                                    | —                                                                                                    |
| Requester Management       | `GET /cm/requesters`, `POST /cm/requesters`, `PATCH /cm/requesters/{id}/deactivate`, `GET /cm/requesters/{id}`, `PUT /cm/requesters/{id}` | Same pattern as Agent Management in Phase 2 (the same table/form component pattern can be reused) |
| Ticket List (Read-Only) | `GET /cm/tickets`                                                                                             | ⚠️ **Do not add any link to the ticket chat page** — this role has no access to chat Endpoints at all (403/404) |

**✅ Phase 5 Definition of Done:**

- The ClientManager user sees no write/status-change buttons on tickets.
- Attempting to manipulate the URL directly toward a chat page (which does not exist) is handled with an appropriate error page, not a UI crash.

---

## 🔄 6. Phase 6 — Final Integration

| Item                          | Description                                                                                                                                      |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Polling Strategy               | Per decision in section 3 of document v6 (row 6), there is no real-time (SignalR) for now. Suggested polling intervals: open chat = every 5 seconds, notifications = every 15–30 seconds, ticket list (open page) = every 30 seconds or Refetch-on-focus |
| Attachment File Handling              | All Endpoints with Attachments use `multipart/form-data` (not Base64 inside JSON) — pay attention to backend file size limits (ask the .NET team) |
| Cross-Role Testing             | With at least 5 real users (one per role), manually test the full cycle: "create ticket → auto-assignment → Agent response → Reassign → Resolve → Reopen"      |
| Generic Error Pages               | 404 page (invalid route), 403 page (unauthorized role attempting to access another area's route), generic server error page (500)                          |

**✅ Phase 6 Definition of Done (i.e. ready for first release):**

- The full ticket cycle (sections 8.2 through 8.5 of scenarios document v1) has been tested End-to-End at least once with real users.
- All error Interceptors (401/400/403/404/409/500) behave correctly.
- Background Polling stops when the Tab is inactive (to avoid unnecessary load on the server) — 💡 suggested, not mentioned in document v6.

---

## ❓ Open Questions for Backend Team Coordination (Ask the .NET Team Before Starting Development)


| # | Question                                                                                              |
| - | ---------------------------------------------------------------------------------------------------- |
| 1 | What is the maximum allowed size and format for attachment files (`Attachments`)?                                          |
| 2 | What is the exact expiration duration for `accessToken` and `refreshToken` (minutes/days)?                                    |
| 3 | Will the Endpoints in this document have exactly these Routes in the actual .NET implementation, or is there a chance the Prefix will change (`/pm`, `/agent`, ...)? |
| 4 | Is CORS configured on the backend for the frontend domain?                                          |
| 5 | Where is the actual backend Swagger UI hosted (if available via `Microsoft.AspNetCore.OpenApi` or `Swashbuckle`)? (for comparison with `openapi.json` in this document) |

---

## ✅ Final Summary

| Phase | Demo-able Outcome                                          |
| --- | -------------------------------------------------------------------------- |
| 0   | Login/Logout/Profile/Notifications work for all 5 roles                    |
| 1   | SuperAdmin creates a Provider with an active subscription                             |
| 2   | ProviderManager creates an Agent and a Client                              |
| 3   | Requester creates a ticket and sends messages in chat                           |
| 4   | Agent responds to a ticket, Reassigns and Resolves                      |
| 5   | ClientManager views their organization's overall status (read-only)                |
| 6   | The entire system is End-to-End tested and stable                                    |
