# FRD: Challenge Progression

**Feature ID:** CHAL  
**PRD Section:** §2 — Challenge Progression  
**Status:** Draft  
**Dependencies:** `frd-auth` (authenticated session, team scoping, role resolution)

---

## 1. Overview

Challenge Progression is the core participant experience of HackboxConsole. Challenges are Markdown files loaded from disk at API startup. They are presented one at a time in a fixed numeric sequence. A coach controls the team's pace via Approve, Revert, and Reset actions. All connected team members see changes in real time via SignalR, with a polling fallback.

This FRD covers:
- Loading and serving challenge content
- Participant challenge viewing (current, completed, locked)
- Coach progression controls (approve, revert, reset)
- Progress tracking (progress bar, celebration screen)
- Real-time state synchronization (SignalR + polling fallback)
- Team progress persistence (JSON on disk)

---

## 2. User Stories

### US-CHAL-01: Participant views current challenge

**As a** participant,  
**I want to** see my team's current challenge rendered as rich HTML,  
**so that** I can read the instructions and work on the task.

**Acceptance criteria:**
- The current challenge Markdown is rendered with headings, code blocks (syntax-highlighted), inline images, lists, and tables
- The challenge number and title (first `# heading` in the Markdown) are displayed
- The participant cannot see challenges beyond the current step

### US-CHAL-02: Participant browses completed challenges

**As a** participant,  
**I want to** browse challenges my team has already completed,  
**so that** I can reference earlier instructions.

**Acceptance criteria:**
- All challenges with a step number less than the current step are browsable
- Completed challenges are visually distinguished from the current challenge
- Selecting a completed challenge displays its full rendered Markdown

### US-CHAL-03: Participant sees locked future challenges

**As a** participant,  
**I want to** see that future challenges exist but are locked,  
**so that** I know how many remain.

**Acceptance criteria:**
- Challenges beyond the current step are shown in the challenge list with a locked indicator
- Locked challenges do not reveal their content (title or body)
- Clicking a locked challenge does not navigate or reveal content

### US-CHAL-04: Participant sees progress bar

**As a** participant,  
**I want to** see a progress bar showing how far my team has progressed,  
**so that** I have a sense of completion.

**Acceptance criteria:**
- The progress bar shows `(currentStep - 1) / totalChallenges` as a percentage
- When at challenge 1 with none completed, progress is 0%
- When all challenges are completed, progress is 100%

### US-CHAL-05: Participant sees celebration screen

**As a** participant,  
**I want to** see a celebration screen when all challenges are completed,  
**so that** I know the hackathon tasks are done.

**Acceptance criteria:**
- When `currentStep > totalChallenges`, the Challenges page shows a celebration screen
- The celebration screen replaces the challenge content area
- The progress bar shows 100%

### US-CHAL-06: Coach approves current challenge

**As a** coach,  
**I want to** approve the current challenge to advance my team to the next one,  
**so that** participants can proceed.

**Acceptance criteria:**
- Clicking "Approve" or pressing `Ctrl+Enter` sends an approve request
- On success, `currentStep` increments by 1
- All connected team members see the new challenge within 2 seconds (SignalR) or 5 seconds (polling)
- The Approve button is available on both the Challenges page and the Solutions page

### US-CHAL-07: Coach reverts to previous challenge

**As a** coach,  
**I want to** revert my team to the previous challenge,  
**so that** they can rework it.

**Acceptance criteria:**
- Clicking "Revert" sends a revert request
- On success, `currentStep` decrements by 1
- All connected team members see the reverted challenge in real time
- The Revert button is available on both the Challenges page and the Solutions page

### US-CHAL-08: Coach resets to challenge 1

**As a** coach,  
**I want to** reset my team to challenge 1,  
**so that** they can start over.

**Acceptance criteria:**
- Clicking "Reset" sends a reset request
- On success, `currentStep` is set to 1
- All connected team members see challenge 1 in real time
- The Reset button is available on both the Challenges page and the Solutions page

### US-CHAL-09: Participant receives real-time updates

**As a** participant,  
**I want to** see challenge changes immediately without refreshing,  
**so that** the experience feels seamless.

**Acceptance criteria:**
- When a coach action changes `currentStep`, connected clients receive the update via SignalR
- If SignalR disconnects, the client polls `GET /api/teams/progress` every 5 seconds
- When SignalR reconnects, polling stops

### US-CHAL-10: Empty state when no challenges loaded

**As a** participant or coach,  
**I want to** see a helpful message when no challenges are loaded,  
**so that** I know the system is not broken.

**Acceptance criteria:**
- If zero challenge files exist at startup, the Challenges page displays: "No challenges loaded — add Markdown files to hackcontent/challenges/"
- Coach controls (Approve, Revert, Reset) are disabled when no challenges are loaded
- The progress bar is hidden

---

## 3. Functional Requirements

### Challenge Loading

| ID | Requirement |
|----|-------------|
| CHAL-001 | On API startup, the server scans `hackcontent/challenges/` for files matching the pattern `challenge-###.md` (three-digit zero-padded number). |
| CHAL-002 | Files are sorted by their numeric suffix in ascending order (001, 002, …). This defines the fixed challenge sequence. |
| CHAL-003 | Each file's raw Markdown content is stored in memory, keyed by its sequence number (1-based integer). |
| CHAL-004 | Files that do not match the `challenge-###.md` naming pattern are ignored silently. |
| CHAL-005 | If the `hackcontent/challenges/` directory does not exist or contains zero matching files, the total challenge count is 0 and the empty-state behavior activates. |
| CHAL-006 | Challenge content is immutable after startup — the server does not watch for file changes. Adding or removing challenges requires a restart. |

### Challenge Retrieval

| ID | Requirement |
|----|-------------|
| CHAL-010 | `GET /api/challenges` returns a JSON array of challenge metadata: `{ challengeNumber, title, status }` where `status` is `"completed"`, `"current"`, or `"locked"`. |
| CHAL-011 | The `title` field is extracted from the first `# heading` in the Markdown. If the file has no `#` heading, the title defaults to `"Challenge {number}"`. |
| CHAL-012 | For challenges with status `"locked"`, the `title` field is `null` — locked challenges do not reveal their title. |
| CHAL-013 | `GET /api/challenges/{number}` returns the full challenge object: `{ challengeNumber, title, contentHtml }` where `contentHtml` is the Markdown rendered to HTML. |
| CHAL-014 | Requesting a challenge with status `"locked"` returns HTTP 403 Forbidden with body `{ "error": "Challenge is locked" }`. |
| CHAL-015 | Requesting a challenge number that does not exist returns HTTP 404 Not Found with body `{ "error": "Challenge not found" }`. |
| CHAL-016 | All `/api/challenges` endpoints require an authenticated session (from `frd-auth`). Unauthenticated requests return HTTP 401. |
| CHAL-017 | Challenge status is computed from the requesting user's team `currentStep`: completed if `number < currentStep`, current if `number == currentStep`, locked if `number > currentStep`. |

### Team Progress

| ID | Requirement |
|----|-------------|
| CHAL-020 | `GET /api/teams/progress` returns the calling user's team progress: `{ teamId, currentStep, totalChallenges, completedChallenges }`. |
| CHAL-021 | `completedChallenges` equals `currentStep - 1`. |
| CHAL-022 | When `currentStep > totalChallenges`, the team has completed all challenges. The response includes `"completed": true`. |
| CHAL-023 | When `totalChallenges` is 0 (no challenges loaded), `currentStep` is 0 and `"completed"` is `false`. |
| CHAL-024 | This endpoint requires an authenticated session. Unauthenticated requests return HTTP 401. |

### Coach Actions

| ID | Requirement |
|----|-------------|
| CHAL-030 | `POST /api/teams/progress/approve` increments the calling coach's team `currentStep` by 1. |
| CHAL-031 | If `currentStep` is already greater than `totalChallenges` (all completed), approve returns HTTP 409 Conflict with body `{ "error": "All challenges already completed" }`. |
| CHAL-032 | If `totalChallenges` is 0, approve returns HTTP 409 Conflict with body `{ "error": "No challenges loaded" }`. |
| CHAL-033 | `POST /api/teams/progress/revert` decrements the calling coach's team `currentStep` by 1. |
| CHAL-034 | If `currentStep` is 1, revert returns HTTP 409 Conflict with body `{ "error": "Already at first challenge" }`. |
| CHAL-035 | If `totalChallenges` is 0, revert returns HTTP 409 Conflict with body `{ "error": "No challenges loaded" }`. |
| CHAL-036 | `POST /api/teams/progress/reset` sets the calling coach's team `currentStep` to 1. |
| CHAL-037 | If `currentStep` is already 1, reset returns HTTP 200 (idempotent — no error). |
| CHAL-038 | If `totalChallenges` is 0, reset returns HTTP 409 Conflict with body `{ "error": "No challenges loaded" }`. |
| CHAL-039 | All three endpoints require the `coach` or `organizer` role. Participants calling these endpoints receive HTTP 403 Forbidden with body `{ "error": "Insufficient permissions" }`. |
| CHAL-040 | All three endpoints require an authenticated session. Unauthenticated requests return HTTP 401. |
| CHAL-041 | On successful approve, revert, or reset, the server persists the updated `currentStep` to the team's JSON progress file on disk before returning the HTTP response. |
| CHAL-042 | On successful approve, revert, or reset, the server broadcasts a `progressUpdated` event to all SignalR clients in the team's group. |

### Markdown Rendering

| ID | Requirement |
|----|-------------|
| CHAL-050 | The frontend renders challenge Markdown to HTML supporting: headings (`#` through `######`), fenced code blocks with syntax highlighting (language hint from the info string), inline and reference images, ordered and unordered lists, tables (pipe-delimited), bold, italic, inline code, and links. |
| CHAL-051 | Code blocks use syntax highlighting based on the language specified in the fenced code block info string (e.g., ` ```powershell `). If no language is specified, the block renders as plain preformatted text. |
| CHAL-052 | Image paths in Markdown are resolved relative to the challenge file's location. The API serves images from `hackcontent/challenges/` via `GET /api/challenges/media/{filename}`. |
| CHAL-053 | If a Markdown file contains malformed syntax, the renderer produces best-effort HTML — it never throws an error or produces an empty page. |

---

## 4. API Endpoints

### REST Endpoints

| Method | Path | Auth | Role | Description |
|--------|------|------|------|-------------|
| `GET` | `/api/challenges` | Required | Any | List all challenges with status relative to team progress |
| `GET` | `/api/challenges/{number}` | Required | Any | Get a single challenge's rendered content (403 if locked) |
| `GET` | `/api/challenges/media/{filename}` | Required | Any | Serve a static media file from `hackcontent/challenges/` |
| `GET` | `/api/teams/progress` | Required | Any | Get the calling user's team progress |
| `POST` | `/api/teams/progress/approve` | Required | Coach, Organizer | Advance to next challenge |
| `POST` | `/api/teams/progress/revert` | Required | Coach, Organizer | Go back to previous challenge |
| `POST` | `/api/teams/progress/reset` | Required | Coach, Organizer | Reset to challenge 1 |

### Response Schemas

**`GET /api/challenges` — 200 OK**
```json
[
  { "challengeNumber": 1, "title": "Azure Migration & Modernization MicroHack", "status": "completed" },
  { "challengeNumber": 2, "title": "Build a Business Case", "status": "current" },
  { "challengeNumber": 3, "title": null, "status": "locked" }
]
```

**`GET /api/challenges/{number}` — 200 OK**
```json
{
  "challengeNumber": 2,
  "title": "Build a Business Case",
  "contentHtml": "<h1>Build a Business Case</h1><p>...</p>"
}
```

**`GET /api/teams/progress` — 200 OK**
```json
{
  "teamId": "team-alpha",
  "currentStep": 2,
  "totalChallenges": 6,
  "completedChallenges": 1,
  "completed": false
}
```

**`POST /api/teams/progress/approve` — 200 OK**
```json
{
  "teamId": "team-alpha",
  "currentStep": 3,
  "totalChallenges": 6,
  "completedChallenges": 2,
  "completed": false
}
```

**Error responses (4xx)**
```json
{ "error": "Challenge is locked" }
```

### SignalR Hub

| Hub Path | `/hubs/progress` |
|----------|-------------------|
| **Client → Server** | — (no client-to-server methods; clients only listen) |
| **Server → Client** | `progressUpdated(TeamProgress progress)` |

**`TeamProgress` payload (same as `GET /api/teams/progress` response):**
```json
{
  "teamId": "team-alpha",
  "currentStep": 3,
  "totalChallenges": 6,
  "completedChallenges": 2,
  "completed": false
}
```

---

## 5. Data Model

### Challenge File Structure

```
hackcontent/
└── challenges/
    ├── challenge-001.md
    ├── challenge-002.md
    ├── challenge-003.md
    ├── challenge-004.md
    ├── challenge-005.md
    └── challenge-006.md
```

**File naming:** `challenge-{NNN}.md` where `{NNN}` is a zero-padded three-digit number starting from 001.  
**Ordering:** The numeric suffix determines the fixed display sequence.  
**Title extraction:** The first line matching `^# (.+)$` becomes the challenge title. If no such line exists, the title defaults to `"Challenge {number}"`.

### In-Memory Challenge Model

```
Challenge {
  number: int           // 1-based sequence number, derived from filename
  title: string         // extracted from first # heading or default
  rawMarkdown: string   // full file content
}
```

### Team Progress JSON (on disk)

**Location:** `data/progress/{teamId}.json`

```json
{
  "teamId": "team-alpha",
  "currentStep": 2,
  "updatedAt": "2026-02-09T14:30:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `teamId` | string | Unique team identifier (from `frd-auth`) |
| `currentStep` | int | 1-based index of the current challenge. Starts at 1. Set to `totalChallenges + 1` when all are completed. |
| `updatedAt` | ISO 8601 string | Timestamp of the last state change |

**Initialization:** When the API starts and a team has no progress file, their `currentStep` defaults to 1.

**Persistence:** The file is written synchronously (flush to disk) after every coach action, before the HTTP response is sent.

---

## 6. Real-Time Behavior

### SignalR Hub: `/hubs/progress`

| Aspect | Detail |
|--------|--------|
| **Transport** | WebSocket preferred, with Server-Sent Events and Long Polling as automatic fallbacks |
| **Group membership** | On connection, the server adds the client to a group named by their `teamId` (derived from the authenticated session). Clients only receive events for their own team. |
| **Event: `progressUpdated`** | Broadcast to the team group after every successful approve, revert, or reset. Payload is the full `TeamProgress` object. |
| **Authentication** | The hub requires the same session cookie used by REST endpoints. Unauthenticated connections are rejected. |

### Client Behavior

| State | Behavior |
|-------|----------|
| **Connected (SignalR)** | Client listens for `progressUpdated` events and updates UI immediately. No polling. |
| **Disconnected** | Client detects disconnection and switches to polling `GET /api/teams/progress` every 5 seconds. |
| **Reconnecting** | SignalR client uses automatic reconnection with exponential backoff. During reconnection attempts, polling continues. |
| **Reconnected** | On successful reconnection, the client fetches the latest progress once (to catch missed events), then stops polling and resumes listening. |

### Latency Target

Coach action → all team members see updated challenge: **< 2 seconds** via SignalR, **< 5 seconds** via polling fallback.

---

## 7. Edge Cases

| ID | Scenario | Expected Behavior |
|----|----------|-------------------|
| CHAL-E01 | No challenge files in `hackcontent/challenges/` | `totalChallenges` = 0, `currentStep` = 0. Challenges page shows empty-state message. Coach controls disabled. Progress bar hidden. |
| CHAL-E02 | Coach approves when already past last challenge | HTTP 409: `"All challenges already completed"`. UI shows brief error notification. |
| CHAL-E03 | Coach reverts when at challenge 1 | HTTP 409: `"Already at first challenge"`. UI shows brief error notification. |
| CHAL-E04 | Coach resets when already at challenge 1 | HTTP 200 (idempotent). No error. UI may show a "Already at first challenge" info notification. |
| CHAL-E05 | Malformed Markdown (unclosed fences, broken tables) | Markdown renderer produces best-effort HTML. No blank page. No server error. |
| CHAL-E06 | Challenge file with no `#` heading | Title defaults to `"Challenge {number}"`. |
| CHAL-E07 | Gap in file numbering (e.g., 001, 002, 004 — no 003) | Only files that exist are loaded. Sequence is based on sorted filenames. The gap is transparent to users — they see challenges 1, 2, 3 mapped from files 001, 002, 004. |
| CHAL-E08 | Participant calls approve/revert/reset endpoint | HTTP 403: `"Insufficient permissions"`. |
| CHAL-E09 | Unauthenticated request to any challenge endpoint | HTTP 401. |
| CHAL-E10 | Team has no progress file on disk | `currentStep` defaults to 1. Progress file is created on first coach action. |
| CHAL-E11 | Progress file is corrupted (invalid JSON) | Server logs a warning, treats the team as `currentStep = 1`, and overwrites the file on the next coach action. |
| CHAL-E12 | `currentStep` in progress file exceeds `totalChallenges` | Treated as "all completed" — the team sees the celebration screen. |
| CHAL-E13 | SignalR connection drops mid-session | Client switches to polling `GET /api/teams/progress` every 5 seconds. Resumes SignalR on reconnect. |
| CHAL-E14 | Two coaches act simultaneously on the same team | No protection. Last write wins. Both actions are applied sequentially (file-system serialization). No error returned. |
| CHAL-E15 | Challenge file is empty (0 bytes) | Loaded with empty content. Title defaults to `"Challenge {number}"`. Rendered HTML is empty. |

---

## 8. Error Handling

### API Error Responses

| HTTP Status | Condition | Response Body |
|-------------|-----------|---------------|
| 401 Unauthorized | No valid session cookie | `{ "error": "Authentication required" }` |
| 403 Forbidden | Participant calls coach endpoint | `{ "error": "Insufficient permissions" }` |
| 403 Forbidden | Request for locked challenge content | `{ "error": "Challenge is locked" }` |
| 404 Not Found | Challenge number does not exist | `{ "error": "Challenge not found" }` |
| 404 Not Found | Media file not found | `{ "error": "File not found" }` |
| 409 Conflict | Approve when all completed | `{ "error": "All challenges already completed" }` |
| 409 Conflict | Revert when at challenge 1 | `{ "error": "Already at first challenge" }` |
| 409 Conflict | Approve/Revert/Reset with no challenges loaded | `{ "error": "No challenges loaded" }` |
| 500 Internal Server Error | Disk write failure for progress file | `{ "error": "Failed to save progress" }` |

### Frontend Error Handling

| Scenario | Behavior |
|----------|----------|
| Coach action returns 409 | Show a brief MUI Snackbar notification with the error message. Auto-dismiss after 4 seconds. |
| Coach action returns 500 | Show a brief MUI Snackbar notification: "Something went wrong. Please try again." Auto-dismiss after 4 seconds. |
| Network failure on coach action | Show MUI Snackbar: "Network error. Check your connection." Auto-dismiss after 4 seconds. |
| SignalR disconnects | Show a subtle connection-status indicator (e.g., yellow dot). Begin polling fallback silently. |
| SignalR reconnects | Remove the connection-status indicator. Stop polling. |
| Challenge content fails to render | Display raw Markdown as preformatted text as a fallback. |

---

## 9. Dependencies

| Dependency | FRD | What This Feature Needs |
|------------|-----|-------------------------|
| Authentication & Sessions | `frd-auth` | Authenticated session cookie on every request. Session contains `userId`, `teamId`, and `role`. |
| Team scoping | `frd-auth` | The `teamId` from the session determines which progress file to read/write and which SignalR group to join. |
| Role resolution | `frd-auth` | The `role` from the session (`participant`, `coach`, `organizer`) determines access to coach action endpoints. |

---

## 10. Non-Functional Notes

| Aspect | Detail |
|--------|--------|
| **Performance** | Challenge Markdown is loaded into memory once at startup. No disk reads on `GET /api/challenges`. |
| **Concurrency** | Progress file writes use a per-team lock to prevent partial writes. No distributed locking needed (single instance). |
| **Scalability** | Designed for single-instance deployment with ≤ 20 teams and ≤ 100 concurrent connections. |
| **Data durability** | Progress files are flushed to disk synchronously. Acceptable data loss window: zero (write-before-respond). |
