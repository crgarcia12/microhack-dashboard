@timer
Feature: Timer & Challenge Timing
  The system tracks time in two independent ways:
  1. Automatic (background) timing — records per-challenge durations driven by coach actions
  2. Manual timer (stopwatch) — a visible per-team stopwatch with start/stop/reset controls

  Background:
    Given the system has a set of challenges numbered 1 through 5
    And a team "team-alpha" exists with currentChallenge 0

  # ──────────────────────────────────────────────
  # Automatic (background) timing
  # ──────────────────────────────────────────────

  @automatic-timing @TMR-001 @TMR-004
  Scenario: Background timer starts on first challenge begin
    Given team "team-alpha" has timerStartedAt null
    When the coach approves challenge 1 for team "team-alpha"
    Then timerStartedAt for team "team-alpha" should be set to the current UTC timestamp
    And challengeTimes for team "team-alpha" should contain key "1"

  @automatic-timing @TMR-001 @TMR-004
  Scenario: timerStartedAt is not overwritten on subsequent approvals
    Given team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    And team "team-alpha" is on challenge 2
    When the coach approves challenge 2 for team "team-alpha"
    Then timerStartedAt for team "team-alpha" should not equal "2025-07-17T09:00:00Z"
    And timerStartedAt for team "team-alpha" should be set to the current UTC timestamp

  @automatic-timing @TMR-002 @TMR-003
  Scenario: Challenge duration recorded as integer seconds on approve
    Given team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    And team "team-alpha" is on challenge 1
    And the current time is "2025-07-17T09:05:12.750Z"
    When the coach approves challenge 1 for team "team-alpha"
    Then challengeTimes for team "team-alpha" should have key "1" with value 312
    And the value should be an integer truncated not rounded

  @automatic-timing @TMR-003
  Scenario: Timer for next challenge starts immediately upon approval
    Given team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    And team "team-alpha" is on challenge 1
    When the coach approves challenge 1 for team "team-alpha"
    Then timerStartedAt for team "team-alpha" should be updated to now
    And the automatic timer should be running for challenge 2

  @automatic-timing @TMR-003 @TMR-007
  Scenario: Approve the last challenge stops the automatic timer
    Given team "team-alpha" is on the last challenge 5
    And team "team-alpha" has timerStartedAt "2025-07-17T09:40:00Z"
    When the coach approves challenge 5 for team "team-alpha"
    Then challengeTimes for team "team-alpha" should have key "5"
    And timerStartedAt for team "team-alpha" should be null
    And no new automatic timer should start

  @automatic-timing @TMR-005
  Scenario: Recorded time removed on revert
    Given team "team-alpha" is on challenge 3
    And challengeTimes for team "team-alpha" contains {"1": 312, "2": 485}
    When the coach reverts team "team-alpha" from challenge 3 to challenge 2
    Then challengeTimes for team "team-alpha" should not have key "2"
    And challengeTimes for team "team-alpha" should still have key "1" with value 312
    And timerStartedAt for team "team-alpha" should be set to now

  @automatic-timing @TMR-005
  Scenario: Revert restarts timer from zero for reverted challenge
    Given team "team-alpha" is on challenge 2
    And team "team-alpha" has timerStartedAt "2025-07-17T09:10:00Z"
    When the coach reverts team "team-alpha" from challenge 2 to challenge 1
    Then timerStartedAt for team "team-alpha" should be set to now
    And the automatic timer should be running for challenge 1

  @automatic-timing @TMR-005
  Scenario: Revert succeeds when no recorded time exists for the target challenge
    Given team "team-alpha" is on challenge 2
    And challengeTimes for team "team-alpha" is empty
    When the coach reverts team "team-alpha" from challenge 2 to challenge 1
    Then the revert should succeed
    And timerStartedAt for team "team-alpha" should be set to now

  @automatic-timing @TMR-006
  Scenario: All recorded times cleared on reset
    Given team "team-alpha" is on challenge 3
    And challengeTimes for team "team-alpha" contains {"1": 312, "2": 485}
    And team "team-alpha" has timerStartedAt "2025-07-17T09:15:00Z"
    When the coach resets team "team-alpha"
    Then challengeTimes for team "team-alpha" should be an empty object
    And timerStartedAt for team "team-alpha" should be null

  @automatic-timing @TMR-006
  Scenario: Automatic timer does not restart after reset until next approve
    Given the coach has reset team "team-alpha"
    Then timerStartedAt for team "team-alpha" should be null
    And the automatic timer should not be running
    When the coach approves challenge 1 for team "team-alpha"
    Then timerStartedAt for team "team-alpha" should be set to the current UTC timestamp

  @automatic-timing @TMR-007
  Scenario: Challenge times persisted to team state file on approve
    Given team "team-alpha" is on challenge 1
    And team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    When the coach approves challenge 1 for team "team-alpha"
    Then the team state file for "team-alpha" should contain the updated challengeTimes

  @automatic-timing @TMR-007
  Scenario: Challenge times persisted to team state file on revert
    Given team "team-alpha" is on challenge 2
    And challengeTimes for team "team-alpha" contains {"1": 312}
    When the coach reverts team "team-alpha" from challenge 2 to challenge 1
    Then the team state file for "team-alpha" should reflect the deleted challengeTimes entry

  @automatic-timing @TMR-007
  Scenario: Challenge times persisted to team state file on reset
    Given team "team-alpha" is on challenge 3
    When the coach resets team "team-alpha"
    Then the team state file for "team-alpha" should have challengeTimes as empty object

  @automatic-timing @TMR-008
  Scenario: GET /api/timer returns automatic timing state
    Given team "team-alpha" is authenticated
    And team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    And challengeTimes for team "team-alpha" contains {"1": 312, "2": 485}
    When the user requests GET /api/timer
    Then the response status should be 200
    And the response body "automatic.timerStartedAt" should be "2025-07-17T09:00:00Z"
    And the response body "automatic.challengeTimes" should contain {"1": 312, "2": 485}

  @automatic-timing @TMR-009
  Scenario: No separate endpoint triggers automatic timing
    Given team "team-alpha" is authenticated
    When the user sends POST /api/timer/automatic/start
    Then the response status should be 404

  # ──────────────────────────────────────────────
  # Edge cases — automatic timing
  # ──────────────────────────────────────────────

  @automatic-timing @TMR-003 @TMR-004
  Scenario: EC-1 — Approve when timerStartedAt is null records 0 seconds
    Given team "team-alpha" has timerStartedAt null
    And team "team-alpha" is on challenge 1
    When the coach approves challenge 1 for team "team-alpha"
    Then timerStartedAt for team "team-alpha" should be set to now
    And challengeTimes for team "team-alpha" should have key "1" with value 0

  @automatic-timing @TMR-006
  Scenario: EC-3 — Reset when already on challenge 1 with no recorded times is idempotent
    Given team "team-alpha" is on challenge 1
    And challengeTimes for team "team-alpha" is empty
    And team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    When the coach resets team "team-alpha"
    Then challengeTimes for team "team-alpha" should be an empty object
    And timerStartedAt for team "team-alpha" should be null

  # ──────────────────────────────────────────────
  # Manual timer (stopwatch) — participant/coach
  # ──────────────────────────────────────────────

  @manual-timer @TMR-010
  Scenario: Manual timer initializes with default stopped state
    Given team "team-alpha" is newly created
    Then the manualTimer for team "team-alpha" should have status "stopped"
    And the manualTimer for team "team-alpha" should have startedAt null
    And the manualTimer for team "team-alpha" should have elapsed 0

  @manual-timer @TMR-011
  Scenario: Start the manual stopwatch
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "stopped"
    When the user sends POST /api/timer/manual/start
    Then the response status should be 200
    And the response body "status" should be "running"
    And the response body "startedAt" should be a valid ISO 8601 timestamp
    And the response body "elapsed" should be 0

  @manual-timer @TMR-011
  Scenario: EC-6 — Start when already running returns 409
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "running"
    When the user sends POST /api/timer/manual/start
    Then the response status should be 409
    And the response body "error" should be "Timer is already running"

  @manual-timer @TMR-012
  Scenario: Stop the manual stopwatch records elapsed time
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "running"
    And the manualTimer startedAt is "2025-07-17T09:15:00Z"
    And the manualTimer elapsed is 100
    And the current time is "2025-07-17T09:24:07.500Z"
    When the user sends POST /api/timer/manual/stop
    Then the response status should be 200
    And the response body "status" should be "stopped"
    And the response body "startedAt" should be null
    And the response body "elapsed" should be 647

  @manual-timer @TMR-012
  Scenario: EC-5 — Stop when already stopped returns 409
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "stopped"
    When the user sends POST /api/timer/manual/stop
    Then the response status should be 409
    And the response body "error" should be "Timer is already stopped"

  @manual-timer @TMR-013
  Scenario: Reset the manual stopwatch to zero
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "running"
    And the manualTimer startedAt is "2025-07-17T09:15:00Z"
    And the manualTimer elapsed is 200
    When the user sends POST /api/timer/manual/reset
    Then the response status should be 200
    And the response body "status" should be "stopped"
    And the response body "startedAt" should be null
    And the response body "elapsed" should be 0

  @manual-timer @TMR-013
  Scenario: Reset from stopped state succeeds
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "stopped"
    And the manualTimer elapsed is 300
    When the user sends POST /api/timer/manual/reset
    Then the response status should be 200
    And the response body "elapsed" should be 0

  @manual-timer @TMR-013
  Scenario: EC-4 — Reset when already at zero and stopped is idempotent
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "stopped"
    And the manualTimer elapsed is 0
    When the user sends POST /api/timer/manual/reset
    Then the response status should be 200
    And the response body "status" should be "stopped"
    And the response body "elapsed" should be 0

  @manual-timer @TMR-014
  Scenario: GET /api/timer/manual returns manual timer state
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "running"
    And the manualTimer startedAt is "2025-07-17T09:15:00Z"
    And the manualTimer elapsed is 0
    When the user requests GET /api/timer/manual
    Then the response status should be 200
    And the response body "status" should be "running"
    And the response body "startedAt" should be "2025-07-17T09:15:00Z"
    And the response body "elapsed" should be 0

  @manual-timer @TMR-015
  Scenario: Manual timer state persisted on start
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "stopped"
    When the user sends POST /api/timer/manual/start
    Then the team state file for "team-alpha" should have manualTimer status "running"

  @manual-timer @TMR-015
  Scenario: Manual timer state persisted on stop
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "running"
    When the user sends POST /api/timer/manual/stop
    Then the team state file for "team-alpha" should have manualTimer status "stopped"

  @manual-timer @TMR-015
  Scenario: Manual timer state persisted on reset
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "running"
    When the user sends POST /api/timer/manual/reset
    Then the team state file for "team-alpha" should have manualTimer elapsed 0

  @manual-timer @TMR-008
  Scenario: GET /api/timer returns manual timer state alongside automatic
    Given team "team-alpha" is authenticated
    And the manualTimer for team "team-alpha" has status "running"
    When the user requests GET /api/timer
    Then the response status should be 200
    And the response body should have an "automatic" object
    And the response body should have a "manual" object
    And the response body "manual.status" should be "running"

  @manual-timer
  Scenario: Manual timer requires authentication
    Given the user is not authenticated
    When the user sends POST /api/timer/manual/start
    Then the response status should be 401
    And the response body "error" should be "Not authenticated"

  # ──────────────────────────────────────────────
  # Timer independence
  # ──────────────────────────────────────────────

  @timer @automatic-timing @manual-timer
  Scenario: Automatic and manual timers are independent
    Given team "team-alpha" is authenticated
    And team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    And the manualTimer for team "team-alpha" has status "stopped"
    When the coach approves challenge 1 for team "team-alpha"
    Then the manualTimer for team "team-alpha" should still have status "stopped"
    And the automatic timer should be running for challenge 2

  @timer @automatic-timing @manual-timer
  Scenario: Manual timer actions do not affect automatic timing
    Given team "team-alpha" is authenticated
    And team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    And the manualTimer for team "team-alpha" has status "stopped"
    When the user sends POST /api/timer/manual/start
    Then timerStartedAt for team "team-alpha" should still be "2025-07-17T09:00:00Z"
    And challengeTimes for team "team-alpha" should not be modified

  @timer @automatic-timing @manual-timer
  Scenario: Reset clears automatic times but does not affect manual timer
    Given team "team-alpha" is on challenge 3
    And challengeTimes for team "team-alpha" contains {"1": 312, "2": 485}
    And the manualTimer for team "team-alpha" has status "running"
    And the manualTimer elapsed is 500
    When the coach resets team "team-alpha"
    Then challengeTimes for team "team-alpha" should be an empty object
    And the manualTimer for team "team-alpha" should still have status "running"
    And the manualTimer elapsed should still be 500

  # ──────────────────────────────────────────────
  # Event organizer — per-team manual timer control
  # ──────────────────────────────────────────────

  @manual-timer @TMR-016
  Scenario: Organizer starts manual timer for a specific team
    Given the user is an authenticated organizer
    And the manualTimer for team "team-alpha" has status "stopped"
    When the organizer sends POST /api/admin/teams/team-alpha/timer/manual/start
    Then the response status should be 200
    And the response body "status" should be "running"

  @manual-timer @TMR-017
  Scenario: Organizer stops manual timer for a specific team
    Given the user is an authenticated organizer
    And the manualTimer for team "team-alpha" has status "running"
    When the organizer sends POST /api/admin/teams/team-alpha/timer/manual/stop
    Then the response status should be 200
    And the response body "status" should be "stopped"

  @manual-timer @TMR-018
  Scenario: Organizer resets manual timer for a specific team
    Given the user is an authenticated organizer
    And the manualTimer for team "team-alpha" has status "running"
    When the organizer sends POST /api/admin/teams/team-alpha/timer/manual/reset
    Then the response status should be 200
    And the response body "elapsed" should be 0

  @manual-timer @TMR-016
  Scenario: EC-10 — Organizer targets non-existent team returns 404
    Given the user is an authenticated organizer
    When the organizer sends POST /api/admin/teams/nonexistent/timer/manual/start
    Then the response status should be 404
    And the response body "error" should be "Team not found"

  @manual-timer @TMR-016
  Scenario: Organizer per-team endpoint requires organizer role
    Given the user is authenticated as a participant
    When the user sends POST /api/admin/teams/team-alpha/timer/manual/start
    Then the response status should be 403
    And the response body "error" should be "Forbidden"

  # ──────────────────────────────────────────────
  # Event organizer — bulk manual timer operations
  # ──────────────────────────────────────────────

  @manual-timer @TMR-019
  Scenario: Bulk start all teams' manual timers
    Given the user is an authenticated organizer
    And team "team-alpha" has manualTimer status "stopped"
    And team "team-beta" has manualTimer status "stopped"
    When the organizer sends POST /api/admin/teams/timer/manual/start
    Then the response status should be 200
    And the results should contain team "team-alpha" with status "ok"
    And the results should contain team "team-beta" with status "ok"

  @manual-timer @TMR-020
  Scenario: Bulk stop all teams' manual timers
    Given the user is an authenticated organizer
    And team "team-alpha" has manualTimer status "running"
    And team "team-beta" has manualTimer status "running"
    When the organizer sends POST /api/admin/teams/timer/manual/stop
    Then the response status should be 200
    And the results should contain team "team-alpha" with status "ok"
    And the results should contain team "team-beta" with status "ok"

  @manual-timer @TMR-021
  Scenario: Bulk reset all teams' manual timers
    Given the user is an authenticated organizer
    And team "team-alpha" has manualTimer status "running"
    And team "team-beta" has manualTimer status "stopped"
    When the organizer sends POST /api/admin/teams/timer/manual/reset
    Then the response status should be 200
    And the results should contain team "team-alpha" with status "ok"
    And the results should contain team "team-beta" with status "ok"

  @manual-timer @TMR-019
  Scenario: EC-9 — Bulk start with mixed states returns per-team results
    Given the user is an authenticated organizer
    And team "team-alpha" has manualTimer status "stopped"
    And team "team-beta" has manualTimer status "running"
    When the organizer sends POST /api/admin/teams/timer/manual/start
    Then the response status should be 200
    And the results should contain team "team-alpha" with status "ok"
    And the results should contain team "team-beta" with status "conflict"
    And the result for team "team-beta" should have error "Timer is already running"

  @manual-timer @TMR-020
  Scenario: EC-9 — Bulk stop with mixed states returns per-team results
    Given the user is an authenticated organizer
    And team "team-alpha" has manualTimer status "running"
    And team "team-beta" has manualTimer status "stopped"
    When the organizer sends POST /api/admin/teams/timer/manual/stop
    Then the response status should be 200
    And the results should contain team "team-alpha" with status "ok"
    And the results should contain team "team-beta" with status "conflict"
    And the result for team "team-beta" should have error "Timer is already stopped"

  @manual-timer @TMR-019 @TMR-020 @TMR-021
  Scenario: Bulk operations require organizer role
    Given the user is authenticated as a coach
    When the user sends POST /api/admin/teams/timer/manual/start
    Then the response status should be 403

  @manual-timer @TMR-022
  Scenario: GET /api/admin/teams/timer returns all teams' timer state
    Given the user is an authenticated organizer
    And team "team-alpha" has timerStartedAt "2025-07-17T09:00:00Z"
    And team "team-alpha" has manualTimer status "running"
    And team "team-beta" has timerStartedAt null
    And team "team-beta" has manualTimer status "stopped"
    When the organizer requests GET /api/admin/teams/timer
    Then the response status should be 200
    And the response body "teams" should be an array
    And the response should include timer data for "team-alpha"
    And the response should include timer data for "team-beta"

  @manual-timer @TMR-022
  Scenario: GET /api/admin/teams/timer requires organizer role
    Given the user is authenticated as a participant
    When the user requests GET /api/admin/teams/timer
    Then the response status should be 403

  # ──────────────────────────────────────────────
  # Edge case — server restart
  # ──────────────────────────────────────────────

  @manual-timer @TMR-010
  Scenario: EC-8 — Manual timer survives server restart
    Given team "team-alpha" has manualTimer status "running"
    And the manualTimer startedAt is "2025-07-17T09:15:00Z"
    When the server restarts
    And the current time is "2025-07-17T09:25:00Z"
    Then the manualTimer for team "team-alpha" should still have status "running"
    And the computed elapsed time should include the downtime period
