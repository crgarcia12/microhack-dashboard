# FRD: Event Organizer Dashboard

**Feature:** Event Organizer Dashboard
**PRD Section:** 6 — Event Organizer Dashboard (Tech Lead)
**Status:** Draft
**Dependencies:** frd-auth (organizer role), frd-challenges (team progress), frd-timer (timer state)

---

## 1. Overview

The Event Organizer Dashboard provides tech leads with a single-pane-of-glass view of every team's status during a hackathon event. From this page, organizers can monitor challenge progress and timer state across all teams, and perform both per-team and bulk operations (advance, revert, reset challenges; start, stop, reset timers).

The dashboard is accessible only to users with the **organizer** (techlead) role. It does not auto-refresh — organizers must manually reload the page to see updated team statuses.

---

## 2. User Stories

### US-DASH-01: View all teams at a glance

**As** an event organizer,
**I want** to see every team's current challenge step, timer status, and elapsed time on a single page,
**So that** I can monitor the event without switching between team views.

**Acceptance criteria:**
- Each team is listed with its name, current challenge step number, total challenges available, and timer status (running / stopped / elapsed time)
- The total number of challenges available is displayed once at the page level
- If no teams are configured, the dashboard shows an empty-state message instead of an empty table

### US-DASH-02: Manage an individual team's challenge

**As** an event organizer,
**I want** to advance, revert, or reset any single team's challenge from the dashboard,
**So that** I can correct mistakes or pace individual teams without affecting others.

**Acceptance criteria:**
- Each team row has Advance, Revert, and Reset action controls
- After a successful action, the affected team's row updates to reflect the new state
- If the action fails (e.g., revert past challenge 1, advance past the last challenge), an error notification is displayed
- The action does not affect any other team

### US-DASH-03: Bulk challenge operations

**As** an event organizer,
**I want** to advance, revert, or reset all teams at once,
**So that** I can synchronize the event pace across all teams efficiently.

**Acceptance criteria:**
- Bulk Advance All, Revert All, and Reset All controls are available outside individual team rows
- Each team is processed independently — a failure on one team does not prevent others from being updated
- After execution, a summary is displayed showing successes and any per-team failures

### US-DASH-04: Manage an individual team's timer

**As** an event organizer,
**I want** to start, stop, or reset any single team's stopwatch from the dashboard,
**So that** I can control timing for teams that need individual attention.

**Acceptance criteria:**
- Each team row has Start, Stop, and Reset timer controls
- Controls are contextual: Start is disabled when the timer is already running; Stop is disabled when stopped
- After a successful action, the team's timer status updates to reflect the new state

### US-DASH-05: Bulk timer operations

**As** an event organizer,
**I want** to start, stop, or reset all teams' stopwatches simultaneously,
**So that** I can synchronize timing across the entire event.

**Acceptance criteria:**
- Bulk Start All, Stop All, and Reset All timer controls are available
- Each team is processed independently — a failure on one team does not prevent others from being updated
- After execution, a summary is displayed showing successes and any per-team failures

### US-DASH-06: Access control

**As** the system,
**I want** to restrict dashboard access to users with the organizer role,
**So that** participants and coaches cannot view or control other teams.

**Acceptance criteria:**
- The dashboard page is only visible in navigation for organizer-role users
- API endpoints return 403 Forbidden for non-organizer users
- Unauthenticated requests return 401 Unauthorized

---

## 3. Functional Requirements

### Monitoring

| ID | Requirement |
|-----|-------------|
| DASH-001 | The dashboard page SHALL display a table of all configured teams with columns: team name, current challenge step (e.g., "3 / 8"), and timer status |
| DASH-002 | Timer status for each team SHALL show one of: "Running" with elapsed time, "Stopped" with elapsed time, or "Not started" |
| DASH-003 | The dashboard SHALL display the total number of challenges available as a summary statistic above the team table |
| DASH-004 | The dashboard SHALL display an empty-state message ("No teams configured") when zero teams exist |
| DASH-005 | The dashboard does NOT auto-refresh — data is fetched once on page load; organizers manually reload to update |

### Per-Team Challenge Operations

| ID | Requirement |
|-----|-------------|
| DASH-010 | Each team row SHALL include an Advance button that moves the team to the next challenge step |
| DASH-011 | Each team row SHALL include a Revert button that moves the team back to the previous challenge step |
| DASH-012 | Each team row SHALL include a Reset button that returns the team to challenge step 1 and clears all progress |
| DASH-013 | If Advance is attempted on a team already on the last challenge, the API SHALL return an error and the UI SHALL display an error notification |
| DASH-014 | If Revert is attempted on a team already on challenge 1, the API SHALL return an error and the UI SHALL display an error notification |

### Bulk Challenge Operations

| ID | Requirement |
|-----|-------------|
| DASH-020 | The dashboard SHALL provide an Advance All button that advances every team by one challenge step |
| DASH-021 | The dashboard SHALL provide a Revert All button that reverts every team by one challenge step |
| DASH-022 | The dashboard SHALL provide a Reset All button that resets every team to challenge step 1 |
| DASH-023 | Bulk operations SHALL process each team independently — a failure on one team MUST NOT prevent the operation from being attempted on remaining teams |
| DASH-024 | After a bulk operation completes, the API SHALL return a result summary listing each team's outcome (success or error with reason) |

### Per-Team Timer Operations

| ID | Requirement |
|-----|-------------|
| DASH-030 | Each team row SHALL include a Start button that starts the team's stopwatch |
| DASH-031 | Each team row SHALL include a Stop button that stops the team's stopwatch |
| DASH-032 | Each team row SHALL include a Reset button that resets the team's stopwatch to 00:00:00 and stops it |
| DASH-033 | The Start button SHALL be disabled when the team's timer is already running |
| DASH-034 | The Stop button SHALL be disabled when the team's timer is already stopped or not started |

### Bulk Timer Operations

| ID | Requirement |
|-----|-------------|
| DASH-040 | The dashboard SHALL provide a Start All Timers button that starts stopwatches for all teams |
| DASH-041 | The dashboard SHALL provide a Stop All Timers button that stops stopwatches for all teams |
| DASH-042 | The dashboard SHALL provide a Reset All Timers button that resets and stops stopwatches for all teams |
| DASH-043 | Bulk timer operations SHALL process each team independently — a failure on one team MUST NOT prevent the operation from being attempted on remaining teams |
| DASH-044 | After a bulk timer operation completes, the API SHALL return a result summary listing each team's outcome (success or error with reason) |

### Access Control

| ID | Requirement |
|-----|-------------|
| DASH-050 | All dashboard API endpoints SHALL require an authenticated session with the organizer (techlead) role |
| DASH-051 | Requests from non-organizer roles SHALL receive a 403 Forbidden response |
| DASH-052 | Unauthenticated requests SHALL receive a 401 Unauthorized response |
| DASH-053 | The dashboard navigation link SHALL only be rendered for users with the organizer role |

---

## 4. API Endpoints

All endpoints are prefixed with `/api/dashboard` and require the organizer role.

### GET /api/dashboard/teams

Returns the status of all teams.

**Response 200:**
```json
{
  "totalChallenges": 8,
  "teams": [
    {
      "teamId": "team-alpha",
      "teamName": "Team Alpha",
      "currentStep": 3,
      "timerStatus": "running",
      "elapsedSeconds": 1842
    },
    {
      "teamId": "team-beta",
      "teamName": "Team Beta",
      "currentStep": 1,
      "timerStatus": "stopped",
      "elapsedSeconds": 0
    }
  ]
}
```

**Error responses:**
- `401 Unauthorized` — no valid session
- `403 Forbidden` — user is not an organizer

---

### POST /api/dashboard/teams/{teamId}/challenge

Advance, revert, or reset a single team's challenge.

**Request body:**
```json
{
  "action": "advance" | "revert" | "reset"
}
```

**Response 200:**
```json
{
  "teamId": "team-alpha",
  "previousStep": 3,
  "currentStep": 4
}
```

**Error responses:**
- `400 Bad Request` — invalid action, or action not possible (e.g., advance past last, revert past first)
  ```json
  { "error": "Cannot advance past the last challenge" }
  ```
- `401 Unauthorized` — no valid session
- `403 Forbidden` — user is not an organizer
- `404 Not Found` — teamId does not exist

---

### POST /api/dashboard/challenge/bulk

Apply a challenge operation to all teams.

**Request body:**
```json
{
  "action": "advance" | "revert" | "reset"
}
```

**Response 200:**
```json
{
  "action": "advance",
  "results": [
    { "teamId": "team-alpha", "success": true, "previousStep": 3, "currentStep": 4 },
    { "teamId": "team-beta", "success": false, "error": "Cannot advance past the last challenge" }
  ]
}
```

**Error responses:**
- `401 Unauthorized` — no valid session
- `403 Forbidden` — user is not an organizer

---

### POST /api/dashboard/teams/{teamId}/timer

Start, stop, or reset a single team's timer.

**Request body:**
```json
{
  "action": "start" | "stop" | "reset"
}
```

**Response 200:**
```json
{
  "teamId": "team-alpha",
  "timerStatus": "running",
  "elapsedSeconds": 1842
}
```

**Error responses:**
- `400 Bad Request` — invalid action, or action not possible (e.g., start an already running timer)
  ```json
  { "error": "Timer is already running" }
  ```
- `401 Unauthorized` — no valid session
- `403 Forbidden` — user is not an organizer
- `404 Not Found` — teamId does not exist

---

### POST /api/dashboard/timer/bulk

Apply a timer operation to all teams.

**Request body:**
```json
{
  "action": "start" | "stop" | "reset"
}
```

**Response 200:**
```json
{
  "action": "start",
  "results": [
    { "teamId": "team-alpha", "success": true, "timerStatus": "running", "elapsedSeconds": 1842 },
    { "teamId": "team-beta", "success": false, "error": "Timer is already running" }
  ]
}
```

**Error responses:**
- `401 Unauthorized` — no valid session
- `403 Forbidden` — user is not an organizer

---

## 5. Data Model

### TeamStatusSummary

Returned by `GET /api/dashboard/teams` for each team:

| Field | Type | Description |
|-------|------|-------------|
| `teamId` | `string` | Unique team identifier |
| `teamName` | `string` | Display name of the team |
| `currentStep` | `int` | 1-based index of the team's current challenge step |
| `timerStatus` | `string` | One of: `"running"`, `"stopped"`, `"not_started"` |
| `elapsedSeconds` | `int` | Total elapsed stopwatch time in seconds |

### DashboardSummary

Top-level response wrapper for the teams endpoint:

| Field | Type | Description |
|-------|------|-------------|
| `totalChallenges` | `int` | Total number of challenges loaded in the system |
| `teams` | `TeamStatusSummary[]` | Status of every configured team |

### BulkOperationResult

Returned by bulk endpoints for each team:

| Field | Type | Description |
|-------|------|-------------|
| `teamId` | `string` | Team that was operated on |
| `success` | `bool` | Whether the operation succeeded for this team |
| `error` | `string?` | Error message if `success` is `false`; omitted on success |
| *(additional fields)* | | Operation-specific fields (e.g., `previousStep`, `currentStep` for challenge ops; `timerStatus`, `elapsedSeconds` for timer ops) |

---

## 6. Edge Cases

| Scenario | Expected Behavior |
|----------|-------------------|
| **No teams configured** | `GET /api/dashboard/teams` returns `{ "totalChallenges": N, "teams": [] }`. The UI shows "No teams configured" empty-state message (DASH-004) |
| **No challenges loaded** | `totalChallenges` is `0`. Per-team `currentStep` is `0`. Challenge operations return 400 with "No challenges available" |
| **Advance on last challenge** | Returns 400: "Cannot advance past the last challenge". UI shows error notification. Other teams in bulk ops are unaffected |
| **Revert on challenge 1** | Returns 400: "Cannot revert before the first challenge". UI shows error notification. Other teams in bulk ops are unaffected |
| **Start an already running timer** | Returns 400: "Timer is already running". UI shows error notification. Other teams in bulk ops are unaffected |
| **Stop an already stopped timer** | Returns 400: "Timer is already stopped". UI shows error notification. Other teams in bulk ops are unaffected |
| **Reset on a not-started timer** | Succeeds as a no-op — timer remains at 00:00:00 in stopped state |
| **Bulk operation with partial failure** | The response includes per-team results. Successful teams are updated; failed teams retain their prior state. UI shows summary with success count and individual failure reasons |
| **Team removed between page load and action** | Returns 404: team not found. Organizer reloads the page to refresh team list |
| **Concurrent organizer actions** | No locking — last write wins. Acceptable for PoC scope |

---

## 7. Error Handling

| Error Condition | HTTP Status | Response Body | UI Behavior |
|----------------|-------------|---------------|-------------|
| Not authenticated | 401 | `{ "error": "Unauthorized" }` | Redirect to login page |
| Not organizer role | 403 | `{ "error": "Forbidden — organizer role required" }` | Display access denied message |
| Team not found | 404 | `{ "error": "Team not found" }` | Error notification; suggest page reload |
| Invalid action value | 400 | `{ "error": "Invalid action. Must be one of: advance, revert, reset" }` | Error notification with message |
| Action not possible | 400 | `{ "error": "<specific reason>" }` | Error notification with the specific reason |
| Server error | 500 | `{ "error": "Internal server error" }` | Generic error notification; suggest retry |

All error responses use a consistent JSON shape: `{ "error": "<message>" }`.

---

## 8. Dependencies

| Dependency | FRD | What is needed |
|------------|-----|----------------|
| Authentication & authorization | frd-auth | Session validation, role extraction (organizer/techlead role check) |
| Challenge progression | frd-challenges | Read team progress (current step), write team progress (advance/revert/reset) |
| Timer state | frd-timer | Read timer state (status, elapsed), write timer state (start/stop/reset) |
| Team configuration | frd-auth | List of configured teams with IDs and display names |

---

## 9. Out of Scope

- Real-time auto-refresh or SignalR push to the dashboard (PRD known limitation)
- Reordering or filtering teams in the dashboard table
- Exporting team progress or timing data
- Confirmation dialogs for destructive operations (Reset, Reset All) — acceptable for PoC
- Undo for bulk operations
