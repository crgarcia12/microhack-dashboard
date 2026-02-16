# FRD: Timer & Challenge Timing

**Feature area:** Timer & Challenge Timing
**PRD section:** 5 — Timer & Challenge Timing
**Dependencies:** frd-auth (team scoping, session identity), frd-challenges (approve / revert / reset trigger timing side-effects)
**Last updated:** 2025-07-17

---

## 1. Overview

HackboxConsole tracks time in two independent ways:

1. **Automatic (background) timing** — the API silently records how long each team spends on each challenge. Timing events are driven entirely by coach actions (approve, revert, reset). No participant interaction is required.
2. **Manual timer page** — a visible stopwatch UI available to participants and coaches. It is a local-only per-team stopwatch with start / stop / reset controls and has no connection to the automatic timing system.

This separation is by design (see PRD §5, "Known limitation"). The automatic timer provides accurate per-challenge durations for reporting. The manual timer is a convenience tool teams use however they wish.

---

## 2. User Stories

### Automatic Timing

#### US-TMR-A1 — Challenge duration recorded on approve

> **As a** coach,
> **I want** the system to automatically record how long my team spent on the current challenge when I approve it,
> **so that** accurate per-challenge durations are captured without manual effort.

**Acceptance criteria:**

- AC1: When the coach approves challenge N, `challengeTimes[N]` is set to the elapsed seconds since challenge N became current.
- AC2: The recorded duration is an integer number of seconds (truncated, not rounded).
- AC3: The timer for the next challenge (N+1) starts immediately upon approval.
- AC4: If challenge N is the last challenge, no new timer starts; the automatic timer stops.

#### US-TMR-A2 — Recorded time removed on revert

> **As a** coach,
> **I want** the recorded time for a challenge to be removed when I revert it,
> **so that** timing data stays consistent with actual progression.

**Acceptance criteria:**

- AC1: When the coach reverts from challenge N+1 back to N, `challengeTimes[N]` is deleted.
- AC2: The automatic timer restarts for challenge N from zero.
- AC3: If no recorded time exists for challenge N (e.g., team was already on challenge N), the revert still succeeds and the timer continues.

#### US-TMR-A3 — All recorded times cleared on reset

> **As a** coach,
> **I want** all challenge times to be cleared when I reset the team to the beginning,
> **so that** the team can start fresh.

**Acceptance criteria:**

- AC1: On reset, `challengeTimes` is set to an empty object `{}`.
- AC2: `timerStartedAt` is set to `null`.
- AC3: The automatic timer does not restart until the team begins challenge 1 again (next approve or explicit start).

#### US-TMR-A4 — Timer starts on first challenge begin

> **As a** participant,
> **I want** the background timer to start automatically when my team begins the first challenge,
> **so that** timing is captured from the very start of the hackathon without any manual action.

**Acceptance criteria:**

- AC1: When the team's `currentChallenge` transitions from 0 to 1 (first approve), `timerStartedAt` is set to the current UTC timestamp.
- AC2: If `timerStartedAt` is already set, it is not overwritten on subsequent approvals.

### Manual Timer (Stopwatch)

#### US-TMR-M1 — Start the manual stopwatch

> **As a** participant or coach,
> **I want** to start a visible stopwatch on the Timer page,
> **so that** I can track time for my own purposes.

**Acceptance criteria:**

- AC1: Clicking "Start" sets `manualTimer.status` to `"running"` and records `manualTimer.startedAt` as the current UTC timestamp.
- AC2: The UI displays elapsed time updating every second in `HH:MM:SS` format.
- AC3: If the stopwatch is already running, the Start button is disabled.

#### US-TMR-M2 — Stop the manual stopwatch

> **As a** participant or coach,
> **I want** to stop the stopwatch,
> **so that** I can pause timing when needed.

**Acceptance criteria:**

- AC1: Clicking "Stop" sets `manualTimer.status` to `"stopped"` and records `manualTimer.elapsed` as the total elapsed seconds.
- AC2: `manualTimer.startedAt` is set to `null`.
- AC3: The displayed time freezes at the stopped value.
- AC4: If the stopwatch is already stopped, the Stop button is disabled.

#### US-TMR-M3 — Reset the manual stopwatch

> **As a** participant or coach,
> **I want** to reset the stopwatch to zero,
> **so that** I can start timing from scratch.

**Acceptance criteria:**

- AC1: Clicking "Reset" sets `manualTimer.status` to `"stopped"`, `manualTimer.elapsed` to `0`, and `manualTimer.startedAt` to `null`.
- AC2: The displayed time shows `00:00:00`.
- AC3: Reset is allowed whether the stopwatch is running or stopped.

#### US-TMR-M4 — Event organizer controls team stopwatches

> **As an** event organizer,
> **I want** to start, stop, and reset stopwatches for individual teams or all teams at once from the dashboard,
> **so that** I can synchronize timing across the event.

**Acceptance criteria:**

- AC1: The dashboard exposes start / stop / reset controls per team.
- AC2: Bulk start / stop / reset applies the action to every team.
- AC3: Each team's stopwatch state is updated independently (partial failures do not roll back others).

---

## 3. Functional Requirements

### Automatic Timing

| ID | Requirement | Source |
|---------|-------------|--------|
| TMR-001 | The API shall maintain a `timerStartedAt` (ISO 8601 UTC timestamp or `null`) per team, representing when the current challenge timer began. | US-TMR-A1, US-TMR-A4 |
| TMR-002 | The API shall maintain a `challengeTimes` object per team, mapping challenge number (string key) to elapsed seconds (integer). | US-TMR-A1 |
| TMR-003 | On **approve**: if `timerStartedAt` is set, compute elapsed seconds as `floor((now - timerStartedAt) / 1000)`, store in `challengeTimes[currentChallenge]`, and set `timerStartedAt` to `now`. If this is the last challenge, set `timerStartedAt` to `null` instead. | US-TMR-A1 |
| TMR-004 | On **approve of challenge 1** (first challenge): if `timerStartedAt` is `null`, set it to `now` before computing elapsed time. This handles the case where the team has not yet started. | US-TMR-A4 |
| TMR-005 | On **revert**: delete `challengeTimes[revertedChallenge]` if it exists. Set `timerStartedAt` to `now` (restart timing for the reverted challenge). | US-TMR-A2 |
| TMR-006 | On **reset**: set `challengeTimes` to `{}` and `timerStartedAt` to `null`. | US-TMR-A3 |
| TMR-007 | Challenge times shall be persisted to the team's JSON state file on every timing mutation (approve, revert, reset). | PRD §Constraints |
| TMR-008 | The `GET /api/timer` endpoint shall return the automatic timing state for the authenticated user's team. | — |
| TMR-009 | Automatic timing mutations are **side-effects** of challenge progression endpoints (approve, revert, reset) defined in frd-challenges. No separate endpoint triggers automatic timing. | — |

### Manual Timer (Stopwatch)

| ID | Requirement | Source |
|---------|-------------|--------|
| TMR-010 | The API shall maintain a `manualTimer` object per team with fields: `status` (`"running"` \| `"stopped"`), `startedAt` (ISO 8601 UTC or `null`), `elapsed` (integer seconds). | US-TMR-M1–M3 |
| TMR-011 | `POST /api/timer/manual/start` — set `status` to `"running"` and `startedAt` to `now`. If already running, return 409 Conflict. | US-TMR-M1 |
| TMR-012 | `POST /api/timer/manual/stop` — compute new elapsed as `manualTimer.elapsed + floor((now - startedAt) / 1000)`, set `status` to `"stopped"`, clear `startedAt`. If already stopped, return 409 Conflict. | US-TMR-M2 |
| TMR-013 | `POST /api/timer/manual/reset` — set `status` to `"stopped"`, `elapsed` to `0`, `startedAt` to `null`. Always succeeds (idempotent). | US-TMR-M3 |
| TMR-014 | `GET /api/timer/manual` — return the manual timer state for the authenticated user's team. | US-TMR-M1–M3 |
| TMR-015 | Manual timer state shall be persisted to the team's JSON state file on every mutation. | PRD §Constraints |

### Event Organizer Endpoints

| ID | Requirement | Source |
|---------|-------------|--------|
| TMR-016 | `POST /api/admin/teams/{teamId}/timer/manual/start` — start the manual timer for a specific team. Requires organizer role. | US-TMR-M4 |
| TMR-017 | `POST /api/admin/teams/{teamId}/timer/manual/stop` — stop the manual timer for a specific team. Requires organizer role. | US-TMR-M4 |
| TMR-018 | `POST /api/admin/teams/{teamId}/timer/manual/reset` — reset the manual timer for a specific team. Requires organizer role. | US-TMR-M4 |
| TMR-019 | `POST /api/admin/teams/timer/manual/start` — bulk start all teams' manual timers. Requires organizer role. Returns per-team results. | US-TMR-M4 |
| TMR-020 | `POST /api/admin/teams/timer/manual/stop` — bulk stop all teams' manual timers. Requires organizer role. Returns per-team results. | US-TMR-M4 |
| TMR-021 | `POST /api/admin/teams/timer/manual/reset` — bulk reset all teams' manual timers. Requires organizer role. Returns per-team results. | US-TMR-M4 |
| TMR-022 | `GET /api/admin/teams/timer` — return automatic timing and manual timer state for all teams. Requires organizer role. | US-TMR-M4 |

---

## 4. API Endpoints

### Team-scoped (participant / coach)

All team-scoped endpoints derive the team from the authenticated session (frd-auth). No `teamId` parameter is accepted.

#### `GET /api/timer`

Returns both automatic and manual timer state for the caller's team.

**Response 200:**
```json
{
  "automatic": {
    "timerStartedAt": "2025-07-17T09:00:00Z",
    "challengeTimes": {
      "1": 312,
      "2": 485
    }
  },
  "manual": {
    "status": "running",
    "startedAt": "2025-07-17T09:15:00Z",
    "elapsed": 0
  }
}
```

**Response 401:** Not authenticated.

#### `POST /api/timer/manual/start`

Start the manual stopwatch.

**Request body:** None.

**Response 200:**
```json
{
  "status": "running",
  "startedAt": "2025-07-17T09:15:00Z",
  "elapsed": 0
}
```

**Response 409:** `{ "error": "Timer is already running" }`
**Response 401:** Not authenticated.

#### `POST /api/timer/manual/stop`

Stop the manual stopwatch.

**Request body:** None.

**Response 200:**
```json
{
  "status": "stopped",
  "startedAt": null,
  "elapsed": 547
}
```

**Response 409:** `{ "error": "Timer is already stopped" }`
**Response 401:** Not authenticated.

#### `POST /api/timer/manual/reset`

Reset the manual stopwatch to zero.

**Request body:** None.

**Response 200:**
```json
{
  "status": "stopped",
  "startedAt": null,
  "elapsed": 0
}
```

**Response 401:** Not authenticated.

### Organizer-scoped (tech lead)

#### `GET /api/admin/teams/timer`

Returns timer state for all teams.

**Response 200:**
```json
{
  "teams": [
    {
      "teamId": "team-1",
      "automatic": {
        "timerStartedAt": "2025-07-17T09:00:00Z",
        "challengeTimes": { "1": 312 }
      },
      "manual": {
        "status": "stopped",
        "startedAt": null,
        "elapsed": 0
      }
    }
  ]
}
```

**Response 401:** Not authenticated.
**Response 403:** Not an organizer.

#### `POST /api/admin/teams/{teamId}/timer/manual/start`
#### `POST /api/admin/teams/{teamId}/timer/manual/stop`
#### `POST /api/admin/teams/{teamId}/timer/manual/reset`

Same semantics as the team-scoped manual timer endpoints, but targeting a specific team.

**Response 200:** Updated manual timer state for the team.
**Response 404:** `{ "error": "Team not found" }`
**Response 409:** Conflict (same rules as team-scoped endpoints).
**Response 401 / 403:** Auth errors.

#### `POST /api/admin/teams/timer/manual/start`
#### `POST /api/admin/teams/timer/manual/stop`
#### `POST /api/admin/teams/timer/manual/reset`

Bulk operations. Apply the action to every team.

**Response 200:**
```json
{
  "results": [
    { "teamId": "team-1", "status": "ok" },
    { "teamId": "team-2", "status": "conflict", "error": "Timer is already running" }
  ]
}
```

**Response 401 / 403:** Auth errors.

---

## 5. Data Model

Timer state is stored within each team's JSON state file on disk (per PRD §Constraints).

### Team state file — timer fields

```json
{
  "teamId": "team-1",
  "currentChallenge": 3,
  "timerStartedAt": "2025-07-17T09:12:30Z",
  "challengeTimes": {
    "1": 312,
    "2": 485
  },
  "manualTimer": {
    "status": "running",
    "startedAt": "2025-07-17T09:15:00Z",
    "elapsed": 0
  }
}
```

### Field definitions

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `timerStartedAt` | `string \| null` | `null` | ISO 8601 UTC timestamp when the current challenge timer began. `null` when no challenge is active. |
| `challengeTimes` | `Record<string, number>` | `{}` | Map of challenge number (as string key) to elapsed seconds. |
| `manualTimer.status` | `"running" \| "stopped"` | `"stopped"` | Current manual stopwatch state. |
| `manualTimer.startedAt` | `string \| null` | `null` | ISO 8601 UTC timestamp when the stopwatch was last started. `null` when stopped. |
| `manualTimer.elapsed` | `number` | `0` | Accumulated elapsed seconds from previous start/stop cycles. When running, total elapsed = `elapsed + (now - startedAt)`. |

### Invariants

- If `manualTimer.status === "running"`, then `manualTimer.startedAt` must not be `null`.
- If `manualTimer.status === "stopped"`, then `manualTimer.startedAt` must be `null`.
- Keys in `challengeTimes` are string representations of challenge numbers (e.g., `"1"`, `"2"`).
- Values in `challengeTimes` are non-negative integers.

---

## 6. Edge Cases

| # | Scenario | Expected Behavior |
|---|----------|-------------------|
| EC-1 | Coach approves when `timerStartedAt` is `null` (timer never started) | Set `timerStartedAt` to `now`, then immediately compute elapsed as 0 seconds. Record `challengeTimes[N] = 0`. Advance normally. |
| EC-2 | Coach reverts when no recorded time exists for the target challenge | Revert succeeds. No entry to delete from `challengeTimes`. Timer restarts for the reverted challenge. |
| EC-3 | Coach resets when team is already on challenge 1 with no recorded times | Reset succeeds (idempotent). `challengeTimes` remains `{}`, `timerStartedAt` set to `null`. |
| EC-4 | Manual timer reset when already at `00:00:00` and stopped | Reset succeeds (idempotent). State unchanged. |
| EC-5 | Manual timer stop when already stopped | Return 409 Conflict with message "Timer is already stopped". |
| EC-6 | Manual timer start when already running | Return 409 Conflict with message "Timer is already running". |
| EC-7 | Approve the last challenge | Record elapsed time for the final challenge. Set `timerStartedAt` to `null`. No new timer starts. |
| EC-8 | Server restarts while manual timer is running | On restart, `manualTimer.startedAt` is still persisted. Elapsed calculation uses `now - startedAt` which will include downtime. This is acceptable for a PoC. |
| EC-9 | Bulk operation with mixed team states (some running, some stopped) | Each team is processed independently. Conflicting teams return `"conflict"` in the results array; others succeed. No rollback. |
| EC-10 | Organizer targets a non-existent team ID | Return 404 with `"Team not found"`. |

---

## 7. Error Handling

| Error | HTTP Status | Response Body | Trigger |
|-------|-------------|---------------|---------|
| Not authenticated | 401 | `{ "error": "Not authenticated" }` | Missing or invalid session cookie |
| Not authorized | 403 | `{ "error": "Forbidden" }` | Non-organizer accessing admin endpoints |
| Team not found | 404 | `{ "error": "Team not found" }` | Organizer targets invalid `teamId` |
| Timer already running | 409 | `{ "error": "Timer is already running" }` | Start when `manualTimer.status === "running"` |
| Timer already stopped | 409 | `{ "error": "Timer is already stopped" }` | Stop when `manualTimer.status === "stopped"` |
| File I/O failure | 500 | `{ "error": "Internal server error" }` | Unable to read/write team state JSON file |

All error responses use the same `{ "error": "<message>" }` shape for consistency.

---

## 8. Dependencies

| Dependency | FRD | What this FRD needs from it |
|------------|-----|-----------------------------|
| Authentication & Team Scoping | frd-auth | Session cookie validation, user→team resolution, role checking (participant / coach / organizer). All timer endpoints rely on the authenticated session to determine the team. |
| Challenge Progression | frd-challenges | Approve, revert, and reset actions trigger automatic timing side-effects (TMR-003 through TMR-006). The challenge endpoints must call into the timer service after mutating challenge state. |

### Dependency direction

- **frd-challenges → frd-timer:** Challenge progression endpoints invoke timer logic as a side-effect. The timer module exposes internal service methods (not HTTP endpoints) for `recordApprove()`, `recordRevert()`, and `recordReset()`.
- **frd-timer → frd-auth:** Timer endpoints use the auth middleware to resolve the team and enforce role checks.
- **frd-timer does NOT depend on frd-challenges** at the API level — it only provides service methods that frd-challenges calls.
