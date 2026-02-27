# FRD: Event Organizer Dashboard

**Feature:** Event Organizer Dashboard
**PRD Section:** 6 — Event Organizer Dashboard (Tech Lead)
**Status:** Draft
**Dependencies:** frd-auth (organizer role), frd-multitenant (microhack lifecycle), frd-challenges (team progress), frd-timer (timer state)

---

## 1. Overview

The Event Organizer Dashboard is a two-level experience for tech leads:

1. A **main dashboard** that lists open microhacks and their key properties.
2. A **selected microhack dashboard** that keeps the existing operational view (team/user tree, event controls, challenge and timer operations).

The dashboard is accessible only to users with the **organizer** (techlead) role. It does not auto-refresh; organizers manually reload to see updated state.

---

## 2. User Stories

### US-DASH-01: View open microhacks at a glance

**As** an event organizer,
**I want** the main dashboard to show open microhacks with key event properties,
**So that** I can quickly choose which hack to manage.

**Acceptance criteria:**
- The main dashboard lists open microhacks (one row per hack)
- Each row includes key properties such as start date and lifecycle state
- If no open microhacks exist, an empty-state message is shown

### US-DASH-02: Create a microhack from the list

**As** an event organizer,
**I want** to create a new microhack directly from the main dashboard,
**So that** I can onboard new events without leaving the page.

**Acceptance criteria:**
- The list view provides a create action
- Creating a microhack with valid required fields succeeds
- The new microhack appears in the list after creation

### US-DASH-03: Enter a selected microhack dashboard

**As** an event organizer,
**I want** to open a specific microhack from the list,
**So that** I can use the existing event controls and team visibility for that hack.

**Acceptance criteria:**
- Selecting a row opens a dashboard scoped to the selected microhack
- The selected dashboard shows the team/user hierarchy and elapsed event context
- The selected dashboard exposes start hack and stop hack lifecycle controls

### US-DASH-04: Manage an individual team's challenge

**As** an event organizer,
**I want** to advance, revert, or reset any single team's challenge inside the selected microhack,
**So that** I can correct mistakes or pace individual teams without affecting others.

### US-DASH-05: Bulk challenge operations

**As** an event organizer,
**I want** to advance, revert, or reset all teams in the selected microhack at once,
**So that** I can synchronize event pace efficiently.

### US-DASH-06: Manage an individual team's timer

**As** an event organizer,
**I want** to start, stop, or reset any single team's stopwatch inside the selected microhack,
**So that** I can control timing for teams that need individual attention.

### US-DASH-07: Bulk timer operations

**As** an event organizer,
**I want** to start, stop, or reset all teams' stopwatches in the selected microhack,
**So that** I can synchronize timing across that event.

### US-DASH-08: Access control

**As** the system,
**I want** to restrict dashboard access to organizer users,
**So that** participants and coaches cannot view or control organizer operations.

---

## 3. Functional Requirements

### Main Dashboard: Microhack List and Entry

| ID | Requirement |
|-----|-------------|
| DASH-001 | The main organizer dashboard SHALL display a list of open microhacks |
| DASH-002 | Each microhack row SHALL include key properties: `name`, `startDate`, `endDate`, and `lifecycleState` |
| DASH-003 | The main dashboard SHALL provide a "Create Microhack" action |
| DASH-004 | Creating a microhack SHALL require valid required fields and reject duplicates |
| DASH-005 | After successful creation, the new microhack SHALL appear in the list without requiring page navigation |
| DASH-006 | If no open microhacks exist, the UI SHALL display an empty-state message ("No open hacks configured") |
| DASH-007 | Each microhack row SHALL include an action to open that microhack's detailed dashboard |
| DASH-008 | The selected microhack context SHALL be preserved in route/state for subsequent scoped operations |

### Selected Microhack Monitoring

| ID | Requirement |
|-----|-------------|
| DASH-009 | Opening a microhack SHALL render a dashboard scoped to that microhack only |
| DASH-015 | The selected dashboard SHALL show a hierarchy tree: Teams -> Hackers (users), or users-only in individual mode |
| DASH-016 | The selected dashboard SHALL show total challenges, per-team current step, timer state, and elapsed time |
| DASH-017 | The selected dashboard SHALL provide start hack and stop hack controls for the selected microhack |
| DASH-018 | The selected dashboard does NOT auto-refresh; organizers manually reload to see updates |

### Per-Team Challenge Operations (Selected Microhack)

| ID | Requirement |
|-----|-------------|
| DASH-010 | Each team row SHALL include an Advance action that moves the team to the next challenge step |
| DASH-011 | Each team row SHALL include a Revert action that moves the team to the previous challenge step |
| DASH-012 | Each team row SHALL include a Reset action that returns the team to challenge step 1 and clears progress |
| DASH-013 | If Advance is attempted on a team already on the last challenge, the API SHALL return an error and the UI SHALL show an error notification |
| DASH-014 | If Revert is attempted on a team already on challenge 1, the API SHALL return an error and the UI SHALL show an error notification |

### Bulk Challenge Operations (Selected Microhack)

| ID | Requirement |
|-----|-------------|
| DASH-020 | The selected dashboard SHALL provide an Advance All action for all teams in the selected microhack |
| DASH-021 | The selected dashboard SHALL provide a Revert All action for all teams in the selected microhack |
| DASH-022 | The selected dashboard SHALL provide a Reset All action for all teams in the selected microhack |
| DASH-023 | Bulk operations SHALL process each team independently; one team failure MUST NOT block others |
| DASH-024 | Bulk operations SHALL return a per-team result summary with success/error details |

### Per-Team Timer Operations (Selected Microhack)

| ID | Requirement |
|-----|-------------|
| DASH-030 | Each team row SHALL include a Start timer action |
| DASH-031 | Each team row SHALL include a Stop timer action |
| DASH-032 | Each team row SHALL include a Reset timer action (00:00:00 and stopped) |
| DASH-033 | Start timer SHALL be disabled when the timer is already running |
| DASH-034 | Stop timer SHALL be disabled when the timer is stopped or not started |

### Bulk Timer Operations (Selected Microhack)

| ID | Requirement |
|-----|-------------|
| DASH-040 | The selected dashboard SHALL provide Start All Timers for teams in the selected microhack |
| DASH-041 | The selected dashboard SHALL provide Stop All Timers for teams in the selected microhack |
| DASH-042 | The selected dashboard SHALL provide Reset All Timers for teams in the selected microhack |
| DASH-043 | Bulk timer operations SHALL process each team independently; one failure MUST NOT block others |
| DASH-044 | Bulk timer operations SHALL return a per-team result summary with success/error details |

### Access Control

| ID | Requirement |
|-----|-------------|
| DASH-050 | All dashboard endpoints SHALL require an authenticated organizer (techlead) session |
| DASH-051 | Non-organizer requests SHALL receive 403 Forbidden |
| DASH-052 | Unauthenticated requests SHALL receive 401 Unauthorized |
| DASH-053 | Organizer navigation SHALL expose the microhack list dashboard; non-organizers SHALL NOT see organizer dashboard links |

---

## 4. API Endpoints

All endpoints are prefixed with `/api/dashboard` and require organizer role.

### GET /api/dashboard/microhacks

Returns open microhacks for the main dashboard list.

**Response 200 (example):**
```json
{
  "microhacks": [
    {
      "microhackId": "mh-2026-01",
      "name": "OpenHack Jan 2026",
      "startDate": "2026-03-01T09:00:00Z",
      "endDate": "2026-03-01T17:00:00Z",
      "lifecycleState": "not_started"
    }
  ]
}
```

### POST /api/dashboard/microhacks

Creates a new microhack from the main dashboard.

**Request body:**
```json
{
  "name": "OpenHack Mar 2026",
  "startDate": "2026-03-01T09:00:00Z",
  "endDate": "2026-03-01T17:00:00Z"
}
```

### GET /api/dashboard/microhacks/{microhackId}/teams

Returns the selected microhack dashboard summary (microhack metadata + teams + challenge/timer states).

### POST /api/dashboard/microhacks/{microhackId}/lifecycle

Starts or stops the selected hack lifecycle.

**Request body:**
```json
{ "action": "start" | "stop" }
```

### POST /api/dashboard/microhacks/{microhackId}/teams/{teamId}/challenge

Per-team challenge action inside selected microhack.

**Request body:**
```json
{ "action": "advance" | "revert" | "reset" }
```

### POST /api/dashboard/microhacks/{microhackId}/challenge/bulk

Bulk challenge action for all teams in selected microhack.

### POST /api/dashboard/microhacks/{microhackId}/teams/{teamId}/timer

Per-team timer action inside selected microhack.

**Request body:**
```json
{ "action": "start" | "stop" | "reset" }
```

### POST /api/dashboard/microhacks/{microhackId}/timer/bulk

Bulk timer action for all teams in selected microhack.

---

## 5. Data Model

### MicrohackListItem

Returned by `GET /api/dashboard/microhacks`.

| Field | Type | Description |
|-------|------|-------------|
| `microhackId` | `string` | Unique microhack identifier |
| `name` | `string` | Display name |
| `startDate` | `datetime` | Scheduled start date/time |
| `endDate` | `datetime` | Scheduled end date/time |
| `lifecycleState` | `string` | One of: `"not_started"`, `"started"`, `"completed"`, `"disabled"` |

### TeamStatusSummary

Returned by `GET /api/dashboard/microhacks/{microhackId}/teams` for each team.

| Field | Type | Description |
|-------|------|-------------|
| `teamId` | `string` | Unique team identifier |
| `teamName` | `string` | Team display name |
| `currentStep` | `int` | 1-based challenge step |
| `timerStatus` | `string` | `"running"`, `"stopped"`, or `"not_started"` |
| `elapsedSeconds` | `int` | Stopwatch elapsed seconds |

### MicrohackDashboardSummary

Top-level response wrapper for selected microhack dashboard data.

| Field | Type | Description |
|-------|------|-------------|
| `microhack` | `MicrohackListItem` | Selected microhack metadata |
| `totalChallenges` | `int` | Total challenge count for this microhack context |
| `teams` | `TeamStatusSummary[]` | Team statuses inside selected microhack |

### BulkOperationResult

Per-team bulk operation result.

| Field | Type | Description |
|-------|------|-------------|
| `teamId` | `string` | Team operated on |
| `success` | `bool` | Whether the operation succeeded |
| `error` | `string?` | Error reason when `success` is `false` |
| *(additional fields)* | | Operation-specific fields (e.g., `previousStep`, `currentStep`, `timerStatus`, `elapsedSeconds`) |

---

## 6. Edge Cases

| Scenario | Expected Behavior |
|----------|-------------------|
| **No open microhacks** | `GET /api/dashboard/microhacks` returns an empty array; UI shows "No open hacks configured" |
| **Duplicate microhack create request** | `POST /api/dashboard/microhacks` returns a validation/conflict error and does not create a second record |
| **Selected microhack no longer exists** | Scoped endpoints return 404 "Microhack not found" and UI prompts user to return to list |
| **No teams in selected microhack** | Selected dashboard shows microhack context and an empty teams state |
| **No challenges loaded** | `totalChallenges` is `0`; challenge actions return 400 with "No challenges available" |
| **Advance on last challenge** | Returns 400 "Cannot advance past the last challenge"; bulk continues for other teams |
| **Revert on challenge 1** | Returns 400 "Cannot revert before the first challenge"; bulk continues for other teams |
| **Start already running timer** | Returns 400 "Timer is already running" |
| **Stop already stopped timer** | Returns 400 "Timer is already stopped" |
| **Bulk operation with partial failure** | Response includes per-team results; successful teams are updated and failures keep prior state |

---

## 7. Error Handling

| Error Condition | HTTP Status | Response Body | UI Behavior |
|----------------|-------------|---------------|-------------|
| Not authenticated | 401 | `{ "error": "Unauthorized" }` | Redirect to login |
| Not organizer role | 403 | `{ "error": "Forbidden — organizer role required" }` | Show access denied |
| Microhack not found | 404 | `{ "error": "Microhack not found" }` | Return to microhack list prompt |
| Team not found in selected microhack | 404 | `{ "error": "Team not found" }` | Show error notification |
| Invalid action value | 400 | `{ "error": "Invalid action" }` | Show error notification |
| Action not possible | 400 | `{ "error": "<specific reason>" }` | Show specific error notification |
| Duplicate/invalid microhack create request | 400/409 | `{ "error": "<validation message>" }` | Keep create form open with validation error |
| Server error | 500 | `{ "error": "Internal server error" }` | Show generic retry message |

All error responses use `{ "error": "<message>" }`.

---

## 8. Dependencies

| Dependency | FRD | What is needed |
|------------|-----|----------------|
| Authentication & authorization | frd-auth | Organizer session validation and role checks |
| Microhack lifecycle and ownership | frd-multitenant | Open microhack discovery, create/enable/disable/start-stop semantics |
| Challenge progression | frd-challenges | Read/write team challenge progression in selected microhack |
| Timer state | frd-timer | Read/write timer state in selected microhack |
| Team and membership data | frd-auth / frd-multitenant | Team-user hierarchy scoped to selected microhack |

---

## 9. Out of Scope

- Cross-microhack bulk operations from the main list page
- Real-time auto-refresh or SignalR push for organizer dashboard views
- Exporting progress/timing data from dashboard
- Undo for bulk operations
