@dashboard @organizer
Feature: Event Organizer Dashboard
  As an event organizer (tech lead),
  I want a single-pane-of-glass view of every team's status
  So that I can monitor and control the hackathon event.

  Background:
    Given the system has 8 challenges loaded
    And the following teams are configured:
      | teamId     | teamName   | currentStep | timerStatus | elapsedSeconds |
      | team-alpha | Team Alpha | 3           | running     | 1842           |
      | team-beta  | Team Beta  | 1           | stopped     | 0              |
      | team-gamma | Team Gamma | 8           | not_started | 0              |
    And I am logged in as a user with the "organizer" role

  # ---------------------------------------------------------------------------
  # Monitoring — DASH-001 through DASH-005
  # ---------------------------------------------------------------------------

  @DASH-001
  Scenario: Dashboard displays all teams with status columns
    When I open the organizer dashboard
    Then I should see a table with rows for "Team Alpha", "Team Beta", and "Team Gamma"
    And each row shows the team name, current challenge step, and timer status

  @DASH-001
  Scenario: Team row shows challenge progress as fraction
    When I open the organizer dashboard
    Then the row for "Team Alpha" should display challenge progress as "3 / 8"
    And the row for "Team Gamma" should display challenge progress as "8 / 8"

  @DASH-002
  Scenario Outline: Timer status displays correctly for each state
    When I open the organizer dashboard
    Then the row for "<teamName>" should show timer status "<displayStatus>"

    Examples:
      | teamName   | displayStatus               |
      | Team Alpha | Running with elapsed time    |
      | Team Beta  | Stopped with elapsed time    |
      | Team Gamma | Not started                  |

  @DASH-003
  Scenario: Dashboard shows total challenges summary
    When I open the organizer dashboard
    Then I should see a summary statistic showing "8" total challenges above the team table

  @DASH-004
  Scenario: Empty state when no teams are configured
    Given no teams are configured
    When I open the organizer dashboard
    Then I should see the message "No teams configured"
    And I should not see the team table

  @DASH-005
  Scenario: Dashboard does not auto-refresh
    When I open the organizer dashboard
    And a team's challenge step changes externally
    Then the dashboard should still show the original data until I manually reload the page

  # ---------------------------------------------------------------------------
  # Per-Team Challenge Operations — DASH-010 through DASH-014
  # ---------------------------------------------------------------------------

  @DASH-010
  Scenario: Advance a single team's challenge
    When I open the organizer dashboard
    And I click the Advance button for "Team Alpha"
    Then the API should move "Team Alpha" from step 3 to step 4
    And the row for "Team Alpha" should display challenge progress as "4 / 8"

  @DASH-011
  Scenario: Revert a single team's challenge
    When I open the organizer dashboard
    And I click the Revert button for "Team Alpha"
    Then the API should move "Team Alpha" from step 3 to step 2
    And the row for "Team Alpha" should display challenge progress as "2 / 8"

  @DASH-012
  Scenario: Reset a single team's challenge
    When I open the organizer dashboard
    And I click the Reset button for "Team Alpha"
    Then the API should reset "Team Alpha" to step 1 and clear all progress
    And the row for "Team Alpha" should display challenge progress as "1 / 8"

  @DASH-010 @DASH-011 @DASH-012
  Scenario: Individual challenge action does not affect other teams
    When I open the organizer dashboard
    And I click the Advance button for "Team Alpha"
    Then the row for "Team Beta" should still display challenge progress as "1 / 8"
    And the row for "Team Gamma" should still display challenge progress as "8 / 8"

  @DASH-013
  Scenario: Advance fails on last challenge
    When I open the organizer dashboard
    And I click the Advance button for "Team Gamma"
    Then the API should return a 400 error with message "Cannot advance past the last challenge"
    And I should see an error notification with "Cannot advance past the last challenge"
    And the row for "Team Gamma" should still display challenge progress as "8 / 8"

  @DASH-014
  Scenario: Revert fails on first challenge
    When I open the organizer dashboard
    And I click the Revert button for "Team Beta"
    Then the API should return a 400 error with message "Cannot revert before the first challenge"
    And I should see an error notification with "Cannot revert before the first challenge"
    And the row for "Team Beta" should still display challenge progress as "1 / 8"

  # ---------------------------------------------------------------------------
  # Bulk Challenge Operations — DASH-020 through DASH-024
  # ---------------------------------------------------------------------------

  @bulk @DASH-020
  Scenario: Advance all teams
    When I open the organizer dashboard
    And I click the "Advance All" button
    Then the API should attempt to advance every team by one step
    And the row for "Team Alpha" should display challenge progress as "4 / 8"
    And the row for "Team Beta" should display challenge progress as "2 / 8"

  @bulk @DASH-021
  Scenario: Revert all teams
    When I open the organizer dashboard
    And I click the "Revert All" button
    Then the API should attempt to revert every team by one step
    And the row for "Team Alpha" should display challenge progress as "2 / 8"

  @bulk @DASH-022
  Scenario: Reset all teams
    When I open the organizer dashboard
    And I click the "Reset All" button
    Then the API should reset every team to step 1
    And the row for "Team Alpha" should display challenge progress as "1 / 8"
    And the row for "Team Beta" should display challenge progress as "1 / 8"
    And the row for "Team Gamma" should display challenge progress as "1 / 8"

  @bulk @DASH-023 @DASH-024
  Scenario: Bulk advance with partial failure shows summary
    When I open the organizer dashboard
    And I click the "Advance All" button
    Then the API should process each team independently
    And the bulk result should show success for "Team Alpha" moving from step 3 to step 4
    And the bulk result should show success for "Team Beta" moving from step 1 to step 2
    And the bulk result should show failure for "Team Gamma" with error "Cannot advance past the last challenge"
    And I should see a summary showing 2 successes and 1 failure

  @bulk @DASH-023 @DASH-024
  Scenario: Bulk revert with partial failure shows summary
    When I open the organizer dashboard
    And I click the "Revert All" button
    Then the API should process each team independently
    And the bulk result should show success for "Team Alpha" moving from step 3 to step 2
    And the bulk result should show failure for "Team Beta" with error "Cannot revert before the first challenge"
    And the bulk result should show success for "Team Gamma" moving from step 8 to step 7
    And I should see a summary showing 2 successes and 1 failure

  # ---------------------------------------------------------------------------
  # Per-Team Timer Operations — DASH-030 through DASH-034
  # ---------------------------------------------------------------------------

  @DASH-030
  Scenario: Start a single team's timer
    When I open the organizer dashboard
    And I click the Start timer button for "Team Beta"
    Then the API should start the timer for "Team Beta"
    And the row for "Team Beta" should show timer status "Running with elapsed time"

  @DASH-031
  Scenario: Stop a single team's timer
    When I open the organizer dashboard
    And I click the Stop timer button for "Team Alpha"
    Then the API should stop the timer for "Team Alpha"
    And the row for "Team Alpha" should show timer status "Stopped with elapsed time"

  @DASH-032
  Scenario: Reset a single team's timer
    When I open the organizer dashboard
    And I click the Reset timer button for "Team Alpha"
    Then the API should reset the timer for "Team Alpha" to 00:00:00 and stop it
    And the row for "Team Alpha" should show timer status "Stopped with elapsed time"

  @DASH-033
  Scenario: Start button is disabled when timer is already running
    When I open the organizer dashboard
    Then the Start timer button for "Team Alpha" should be disabled

  @DASH-034
  Scenario Outline: Stop button is disabled when timer is not running
    When I open the organizer dashboard
    Then the Stop timer button for "<teamName>" should be disabled

    Examples:
      | teamName   |
      | Team Beta  |
      | Team Gamma |

  # ---------------------------------------------------------------------------
  # Bulk Timer Operations — DASH-040 through DASH-044
  # ---------------------------------------------------------------------------

  @bulk @DASH-040
  Scenario: Start all timers
    When I open the organizer dashboard
    And I click the "Start All Timers" button
    Then the API should attempt to start every team's timer

  @bulk @DASH-041
  Scenario: Stop all timers
    When I open the organizer dashboard
    And I click the "Stop All Timers" button
    Then the API should attempt to stop every team's timer

  @bulk @DASH-042
  Scenario: Reset all timers
    When I open the organizer dashboard
    And I click the "Reset All Timers" button
    Then the API should reset and stop every team's timer

  @bulk @DASH-043 @DASH-044
  Scenario: Bulk start timers with partial failure shows summary
    When I open the organizer dashboard
    And I click the "Start All Timers" button
    Then the API should process each team's timer independently
    And the bulk timer result should show failure for "Team Alpha" with error "Timer is already running"
    And the bulk timer result should show success for "Team Beta" with status "running"
    And the bulk timer result should show success for "Team Gamma" with status "running"
    And I should see a summary showing 2 successes and 1 failure

  @bulk @DASH-043 @DASH-044
  Scenario: Bulk stop timers with partial failure shows summary
    Given all teams have their timers running
    When I open the organizer dashboard
    And I click the "Stop All Timers" button
    And "Team Beta" timer is already stopped
    Then the API should process each team's timer independently
    And the bulk timer result should show success for "Team Alpha" with status "stopped"
    And the bulk timer result should show failure for "Team Beta" with error "Timer is already stopped"
    And the bulk timer result should show success for "Team Gamma" with status "stopped"
    And I should see a summary showing 2 successes and 1 failure

  # ---------------------------------------------------------------------------
  # Per-Team Operations via Scenario Outline — DASH-010/011/012, DASH-030/031/032
  # ---------------------------------------------------------------------------

  @DASH-010 @DASH-011 @DASH-012
  Scenario Outline: Individual challenge operation on a team
    When I open the organizer dashboard
    And I perform a "<action>" challenge action on "Team Alpha" via the API
    Then the API should return a 200 response with the updated step for "Team Alpha"
    And the response should include "previousStep" and "currentStep"

    Examples:
      | action  |
      | advance |
      | revert  |
      | reset   |

  @DASH-030 @DASH-031 @DASH-032
  Scenario Outline: Individual timer operation on a team
    When I open the organizer dashboard
    And I perform a "<action>" timer action on "Team Alpha" via the API
    Then the API should return a 200 response with the updated timer status for "Team Alpha"
    And the response should include "timerStatus" and "elapsedSeconds"

    Examples:
      | action |
      | start  |
      | stop   |
      | reset  |

  # ---------------------------------------------------------------------------
  # Bulk Operations via Scenario Outline — DASH-020/021/022, DASH-040/041/042
  # ---------------------------------------------------------------------------

  @bulk @DASH-020 @DASH-021 @DASH-022 @DASH-024
  Scenario Outline: Bulk challenge operation returns per-team results
    When I perform a bulk "<action>" challenge action via the API
    Then the API should return a 200 response with a results array
    And each result should include "teamId" and "success"
    And failed results should include an "error" field

    Examples:
      | action  |
      | advance |
      | revert  |
      | reset   |

  @bulk @DASH-040 @DASH-041 @DASH-042 @DASH-044
  Scenario Outline: Bulk timer operation returns per-team results
    When I perform a bulk "<action>" timer action via the API
    Then the API should return a 200 response with a results array
    And each result should include "teamId" and "success"
    And failed results should include an "error" field

    Examples:
      | action |
      | start  |
      | stop   |
      | reset  |

  # ---------------------------------------------------------------------------
  # Access Control — DASH-050 through DASH-053
  # ---------------------------------------------------------------------------

  @DASH-050
  Scenario: Dashboard API requires organizer role
    Given I am logged in as a user with the "organizer" role
    When I request GET /api/dashboard/teams
    Then the API should return a 200 response

  @DASH-051
  Scenario Outline: Non-organizer roles receive 403
    Given I am logged in as a user with the "<role>" role
    When I request <method> <endpoint>
    Then the API should return a 403 Forbidden response

    Examples:
      | role        | method | endpoint                                  |
      | participant | GET    | /api/dashboard/teams                      |
      | coach       | POST   | /api/dashboard/teams/team-alpha/challenge  |
      | participant | POST   | /api/dashboard/challenge/bulk              |
      | coach       | POST   | /api/dashboard/teams/team-alpha/timer      |
      | participant | POST   | /api/dashboard/timer/bulk                  |

  @DASH-052
  Scenario Outline: Unauthenticated requests receive 401
    Given I am not authenticated
    When I request <method> <endpoint>
    Then the API should return a 401 Unauthorized response

    Examples:
      | method | endpoint                                  |
      | GET    | /api/dashboard/teams                      |
      | POST   | /api/dashboard/teams/team-alpha/challenge  |
      | POST   | /api/dashboard/challenge/bulk              |
      | POST   | /api/dashboard/teams/team-alpha/timer      |
      | POST   | /api/dashboard/timer/bulk                  |

  @DASH-053
  Scenario: Dashboard nav link is hidden for non-organizer users
    Given I am logged in as a user with the "participant" role
    When I view the application navigation
    Then I should not see a link to the organizer dashboard

  @DASH-053
  Scenario: Dashboard nav link is visible for organizer users
    Given I am logged in as a user with the "organizer" role
    When I view the application navigation
    Then I should see a link to the organizer dashboard

  # ---------------------------------------------------------------------------
  # Edge Cases
  # ---------------------------------------------------------------------------

  @dashboard @DASH-004
  Scenario: API returns empty teams array when no teams configured
    Given no teams are configured
    And I am logged in as a user with the "organizer" role
    When I request GET /api/dashboard/teams
    Then the API should return a 200 response with "totalChallenges" and an empty "teams" array

  @dashboard @DASH-013
  Scenario: Advance returns 400 when no challenges are loaded
    Given the system has 0 challenges loaded
    And I am logged in as a user with the "organizer" role
    When I perform a "advance" challenge action on "Team Alpha" via the API
    Then the API should return a 400 error with message "No challenges available"

  @dashboard
  Scenario: Timer reset on a not-started timer succeeds as no-op
    When I open the organizer dashboard
    And I click the Reset timer button for "Team Gamma"
    Then the API should return a 200 response
    And the row for "Team Gamma" should show timer status "Stopped with elapsed time"

  @dashboard
  Scenario: Start already running timer returns error
    When I perform a "start" timer action on "Team Alpha" via the API
    Then the API should return a 400 error with message "Timer is already running"

  @dashboard
  Scenario: Stop already stopped timer returns error
    When I perform a "stop" timer action on "Team Beta" via the API
    Then the API should return a 400 error with message "Timer is already stopped"

  @dashboard
  Scenario: Action on a removed team returns 404
    When I perform a "advance" challenge action on "team-deleted" via the API
    Then the API should return a 404 error with message "Team not found"

  @dashboard
  Scenario: Invalid action value returns 400
    When I send a challenge action "jump" for "Team Alpha" via the API
    Then the API should return a 400 error with message "Invalid action. Must be one of: advance, revert, reset"
