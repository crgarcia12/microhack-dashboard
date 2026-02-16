# FRD-AUTH: Authentication & Sessions

## 1. Overview

This feature provides username/password authentication against a JSON configuration file, server-side session management via cookies, and role-based identity context for all downstream features. It is the foundation of HackboxConsole — every other feature depends on knowing who the user is, what role they have, and which team they belong to.

**Scope:** Login page, login API, logout API, session middleware, session-to-identity resolution, navigation adaptation by role.

**Out of scope:** Self-registration, password reset, account lockout, session expiry, OAuth/SSO.

---

## 2. User Stories

### US-AUTH-1: Log In

**As a** hackathon participant, coach, or event organizer,
**I want to** log in with my username and password,
**so that** I can access the features appropriate to my role.

**Acceptance criteria:**
- Given I am on the login page and I enter a valid username and password, when I click "Login", then I am redirected to the home page and a session cookie is set.
- Given I enter a valid username with different casing (e.g., "Alice" vs "alice"), when I click "Login", then login succeeds (case-insensitive).
- Given I enter an invalid username or wrong password, when I click "Login", then I see "Invalid username or password" and no session is created.
- Given I leave the username or password field empty, when I click "Login", then I see "Please fill in all fields" and no request is sent to the server.
- Given I have just failed a login attempt, when I try again immediately, then the login proceeds without delay or lockout.

### US-AUTH-2: Stay Logged In

**As a** logged-in user,
**I want to** remain authenticated across page refreshes and browser tab reopens,
**so that** I don't have to log in repeatedly during the event.

**Acceptance criteria:**
- Given I am logged in and I refresh the page, then I remain logged in and see my role-appropriate content.
- Given I am logged in and I open a new browser tab to the same URL, then I am still logged in.
- Given the server has restarted since my last request, when I make a request, then my session is invalid and I am redirected to the login page.

### US-AUTH-3: Log Out

**As a** logged-in user,
**I want to** log out,
**so that** my session is terminated and no one else can use it.

**Acceptance criteria:**
- Given I am logged in and I click "Logout", then my session cookie is cleared, my server-side session is deleted, and I am redirected to the login page.
- Given I have logged out and I use the browser back button, then I am redirected to the login page (not shown stale content).

### US-AUTH-4: Role-Adapted Navigation

**As a** logged-in user,
**I want to** see navigation links appropriate to my role,
**so that** I only see sections I have access to.

**Acceptance criteria:**
- Given I am a participant, then I see: Challenges, Credentials, Timer.
- Given I am a coach, then I see: Challenges, Solutions, Credentials, Timer.
- Given I am a techlead (event organizer), then I see: Dashboard, Challenges, Solutions, Credentials, Timer.
- Given I am logged in, then the nav bar displays my username and role.

### US-AUTH-5: Session Identity for API Requests

**As the** system,
**I want to** resolve every API request to a user identity (username, role, team),
**so that** all downstream features can enforce team isolation without the client supplying a team ID.

**Acceptance criteria:**
- Given I make an API request with a valid session cookie, then the server resolves my identity (username, role, teamId) from the session and makes it available to the endpoint handler.
- Given I make an API request without a session cookie or with an invalid/expired session cookie, then the server returns HTTP 401.
- Given I am an event organizer, then my identity has no teamId (organizers span all teams).

---

## 3. Functional Requirements

### Login

| ID | Requirement |
|---|---|
| **AUTH-001** | The system SHALL provide a login page at the root URL (`/`) that displays a username field, a password field, and a "Login" button. |
| **AUTH-002** | The login page SHALL perform client-side validation: if either the username or password field is empty when the user clicks "Login", the page SHALL display "Please fill in all fields" and SHALL NOT send a request to the server. |
| **AUTH-003** | When both fields are populated, clicking "Login" SHALL send a `POST /api/auth/login` request with `{ "username": "<value>", "password": "<value>" }`. |
| **AUTH-004** | The server SHALL compare the submitted username against the `users.json` config file using **case-insensitive** matching (e.g., "Alice", "alice", and "ALICE" all match the same user). |
| **AUTH-005** | The server SHALL compare the submitted password against the matched user's password using **exact (case-sensitive)** string comparison. |
| **AUTH-006** | If no user matches or the password is wrong, the server SHALL return HTTP 401 with body `{ "error": "Invalid username or password" }`. The response SHALL NOT indicate whether the username or password was incorrect. |
| **AUTH-007** | If credentials are valid, the server SHALL create a new session, generate a cryptographically random session ID (minimum 32 hex characters), store the session server-side in memory, and return HTTP 200 with a `Set-Cookie` header containing the session ID. |
| **AUTH-008** | The session cookie SHALL be named `sessionId`, SHALL have `HttpOnly` set to `true`, `SameSite` set to `Strict`, and `Path` set to `/`. In production, `Secure` SHALL be `true`; in development, `Secure` MAY be `false`. |
| **AUTH-009** | On receiving HTTP 200 from the login endpoint, the frontend SHALL redirect the user to the role-appropriate home page: `/challenges` for participants and coaches, `/dashboard` for techleads. |
| **AUTH-010** | If a user who already has an active session logs in again, the server SHALL invalidate the previous session and create a new one (no concurrent sessions per user). |

### Logout

| ID | Requirement |
|---|---|
| **AUTH-011** | The system SHALL provide a `POST /api/auth/logout` endpoint. |
| **AUTH-012** | On logout, the server SHALL delete the session from the in-memory session store and return HTTP 200 with a `Set-Cookie` header that clears the `sessionId` cookie (sets it to empty with `Max-Age=0`). |
| **AUTH-013** | The frontend SHALL redirect to the login page (`/`) after receiving a successful logout response. |
| **AUTH-014** | If `POST /api/auth/logout` is called without a valid session cookie, the server SHALL return HTTP 200 (idempotent — logging out when not logged in is not an error). |

### Session Validation & Identity Resolution

| ID | Requirement |
|---|---|
| **AUTH-015** | Every API request (except `POST /api/auth/login`) SHALL require a valid `sessionId` cookie. If the cookie is missing or the session ID does not exist in the server's session store, the server SHALL return HTTP 401 with body `{ "error": "Unauthorized" }`. |
| **AUTH-016** | The server SHALL maintain an in-memory dictionary mapping session IDs to user identity objects: `{ "username": string, "role": string, "teamId": string | null }`. |
| **AUTH-017** | Sessions SHALL NOT expire based on time. A session remains valid until the user logs out, the user logs in again (AUTH-010), or the server process restarts (which clears in-memory state). |
| **AUTH-018** | The server SHALL expose a `GET /api/auth/me` endpoint that returns the current user's identity: `{ "username": string, "role": string, "teamId": string | null }`. If not authenticated, returns HTTP 401. |

### Role & Navigation

| ID | Requirement |
|---|---|
| **AUTH-019** | The system SHALL recognize exactly three roles: `participant`, `coach`, and `techlead`. Any user in `users.json` with a role value other than these three SHALL be rejected at config load time (server fails to start with an error message identifying the invalid entry). |
| **AUTH-020** | The frontend navigation bar SHALL display the logged-in user's username and role (human-readable label: "Participant", "Coach", "Tech Lead"). |
| **AUTH-021** | The frontend navigation bar SHALL show only the links permitted for the user's role, as defined in US-AUTH-4. |
| **AUTH-022** | If an authenticated user navigates to a URL they do not have role access to (e.g., a participant navigates to `/dashboard`), the frontend SHALL redirect them to their role-appropriate home page. |
| **AUTH-023** | If an unauthenticated user navigates to any page other than `/`, the frontend SHALL redirect them to the login page. |

---

## 4. API Endpoints

### POST /api/auth/login

**Purpose:** Authenticate a user and create a session.

**Request:**
```json
{
  "username": "alice",
  "password": "password123"
}
```

**Success response (200):**
```
Set-Cookie: sessionId=<hex-session-id>; HttpOnly; SameSite=Strict; Path=/
```
```json
{
  "username": "alice",
  "role": "participant",
  "teamId": "team1"
}
```

**Error responses:**

| Status | Condition | Body |
|---|---|---|
| 400 | Missing `username` or `password` in request body | `{ "error": "Username and password are required" }` |
| 401 | No matching user or wrong password | `{ "error": "Invalid username or password" }` |

---

### POST /api/auth/logout

**Purpose:** Destroy the current session.

**Request:** No body. Session identified by `sessionId` cookie.

**Success response (200):**
```
Set-Cookie: sessionId=; HttpOnly; SameSite=Strict; Path=/; Max-Age=0
```
```json
{ "message": "Logged out" }
```

**Error responses:** None — always returns 200 (idempotent).

---

### GET /api/auth/me

**Purpose:** Return the identity of the currently authenticated user.

**Request:** No body. Session identified by `sessionId` cookie.

**Success response (200):**
```json
{
  "username": "alice",
  "role": "participant",
  "teamId": "team1"
}
```

For event organizers:
```json
{
  "username": "adminuser",
  "role": "techlead",
  "teamId": null
}
```

**Error responses:**

| Status | Condition | Body |
|---|---|---|
| 401 | No session cookie or invalid session | `{ "error": "Unauthorized" }` |

---

## 5. Data Model

### User Configuration File: `users.json`

Located at a configurable path (default: `data/users.json`). Loaded once at server startup.

```json
{
  "users": [
    {
      "username": "alice",
      "password": "hunter2",
      "role": "participant",
      "teamId": "team1"
    },
    {
      "username": "bob",
      "password": "coachpass",
      "role": "coach",
      "teamId": "team1"
    },
    {
      "username": "adminuser",
      "password": "adminpass",
      "role": "techlead",
      "teamId": null
    }
  ]
}
```

**Schema constraints:**

| Field | Type | Required | Rules |
|---|---|---|---|
| `username` | string | Yes | Non-empty. Must be unique (case-insensitive). |
| `password` | string | Yes | Non-empty. Plain text (PoC). |
| `role` | string | Yes | One of: `participant`, `coach`, `techlead`. |
| `teamId` | string \| null | Yes | Non-null for `participant` and `coach`. Must be `null` for `techlead`. |

### In-Memory Session Store

A server-side dictionary. Not persisted to disk — cleared on server restart.

```
Dictionary<string, SessionEntry>
```

```json
{
  "a3f9c1...": {
    "username": "alice",
    "role": "participant",
    "teamId": "team1",
    "createdAt": "2025-01-15T10:30:00Z"
  }
}
```

| Field | Type | Description |
|---|---|---|
| Key (session ID) | string | Cryptographically random, minimum 32 hex characters. |
| `username` | string | The canonical (lowercased) username. |
| `role` | string | The user's role from config. |
| `teamId` | string \| null | The user's team from config. Null for techlead. |
| `createdAt` | ISO 8601 string | Timestamp of session creation (for diagnostics only — not used for expiry). |

---

## 6. Edge Cases

| ID | Scenario | Expected Behavior |
|---|---|---|
| **EDGE-001** | `users.json` file does not exist at startup | Server fails to start. Logs error: "Users config file not found at {path}". |
| **EDGE-002** | `users.json` exists but is empty (`{ "users": [] }`) | Server starts successfully. All login attempts return 401. |
| **EDGE-003** | `users.json` contains duplicate usernames (case-insensitive, e.g., "Alice" and "alice") | Server fails to start. Logs error: "Duplicate username detected: {username}". |
| **EDGE-004** | `users.json` contains a user with an invalid role (not participant/coach/techlead) | Server fails to start. Logs error: "Invalid role '{role}' for user '{username}'". |
| **EDGE-005** | `users.json` contains a participant or coach with `teamId: null` | Server fails to start. Logs error: "User '{username}' with role '{role}' must have a teamId". |
| **EDGE-006** | `users.json` contains a techlead with a non-null `teamId` | Server fails to start. Logs error: "User '{username}' with role 'techlead' must not have a teamId". |
| **EDGE-007** | Client sends a request with a `sessionId` cookie that is syntactically valid but not in the session store | Server returns HTTP 401. |
| **EDGE-008** | Client sends a request with a malformed `sessionId` cookie (non-hex, empty, extremely long) | Server returns HTTP 401. No crash, no stack trace in response. |
| **EDGE-009** | Server restarts while users have active sessions | All sessions are lost (in-memory only). Next request from any previously-authenticated user returns 401. Frontend detects 401 and redirects to login page. |
| **EDGE-010** | User logs in from two different browsers simultaneously | Second login invalidates the first session (AUTH-010). First browser's next request gets 401. |
| **EDGE-011** | `users.json` contains malformed JSON | Server fails to start. Logs error: "Failed to parse users config: {parse error details}". |
| **EDGE-012** | Login request body is not valid JSON or is missing fields | Server returns HTTP 400 with `{ "error": "Username and password are required" }`. |

---

## 7. Error Handling

Every failure mode and the user-visible result:

| Failure | HTTP Status | API Response | What the User Sees |
|---|---|---|---|
| Empty username or password (client-side) | N/A (no request sent) | N/A | Inline message: "Please fill in all fields" |
| Empty username or password (server-side, if client validation bypassed) | 400 | `{ "error": "Username and password are required" }` | "Username and password are required" |
| Wrong username | 401 | `{ "error": "Invalid username or password" }` | "Invalid username or password" |
| Wrong password | 401 | `{ "error": "Invalid username or password" }` | "Invalid username or password" |
| Request to protected endpoint without session | 401 | `{ "error": "Unauthorized" }` | Redirected to login page |
| Request with invalid/expired session cookie | 401 | `{ "error": "Unauthorized" }` | Redirected to login page |
| Participant accesses organizer-only page | N/A (client-side redirect) | N/A | Redirected to `/challenges` |
| Server error during login (unexpected) | 500 | `{ "error": "Internal server error" }` | "Something went wrong. Please try again." |

---

## 8. Dependencies

**None.** Authentication & Sessions is the foundational feature. All other features depend on this:

- **Challenge Progression** depends on session identity for team scoping
- **Credentials Display** depends on session identity for team scoping
- **Timer** depends on session identity for team scoping
- **Event Organizer Dashboard** depends on role resolution for access control
- **Solutions Viewing** depends on role resolution for access control

---

## 9. Traceability

| PRD Section | FRD Requirement(s) |
|---|---|
| Feature 1: User Login & Role-Based Experience | AUTH-001 through AUTH-010, AUTH-019 through AUTH-023 |
| Feature 1: Error handling | AUTH-002, AUTH-006, EDGE-007 through EDGE-012 |
| Feature 7: Team Isolation — session scoping | AUTH-015, AUTH-016, AUTH-018 |
| Feature 7: Team Isolation — no client-supplied team ID | AUTH-016 (teamId from session, not request) |
| Feature 7: Organizers span all teams | AUTH-018 (teamId: null for techlead) |
| Constraints: Sessions do not expire | AUTH-017 |
| Constraints: No self-registration | Explicitly out of scope in §1 |
