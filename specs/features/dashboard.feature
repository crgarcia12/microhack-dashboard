@dashboard @organizer
Feature: Event Organizer Dashboard
  As an event organizer (tech lead),
  I want to manage microhacks from a list and operate one selected hack at a time
  So that I can control hackathon flow with scoped, event-level visibility.

  Background:
    Given I am logged in as a user with the "organizer" role
    And the system has the following open microhacks:
      | microhackId | name              | startDate            | endDate              | lifecycleState |
      | mh-open-1   | OpenHack Spring   | 2026-03-01T09:00:00Z | 2026-03-01T17:00:00Z | not_started    |
      | mh-open-2   | OpenHack Security | 2026-03-02T09:00:00Z | 2026-03-02T17:00:00Z | started        |
    And microhack "mh-open-1" has 8 challenges loaded
    And microhack "mh-open-1" has the following teams:
      | teamId     | teamName   | currentStep | timerStatus | elapsedSeconds |
      | team-alpha | Team Alpha | 3           | running     | 1842           |
      | team-beta  | Team Beta  | 1           | stopped     | 0              |
      | team-gamma | Team Gamma | 8           | not_started | 0              |

  # ---------------------------------------------------------------------------
  # Main dashboard: open microhack list and entry — DASH-001 through DASH-009
  # ---------------------------------------------------------------------------

  @DASH-001 @DASH-002
  Scenario: Main dashboard lists open microhacks with key properties
    When I open the organizer dashboard
    Then I should see a list containing "OpenHack Spring" and "OpenHack Security"
    And each row should show name, start date, end date, and lifecycle state

  @DASH-003 @DASH-004 @DASH-005
  Scenario: Create a new microhack from the list
    When I open the organizer dashboard
    And I create a microhack with:
      | name         | startDate            | endDate              |
      | OpenHack AI  | 2026-03-03T09:00:00Z | 2026-03-03T17:00:00Z |
    Then the API should create the microhack successfully
    And the list should include "OpenHack AI"

  @DASH-006
  Scenario: Empty state when no open microhacks are configured
    Given no open microhacks are configured
    When I open the organizer dashboard
    Then I should see the message "No open hacks configured"
    And I should not see any microhack rows

  @DASH-007 @DASH-008 @DASH-009
  Scenario: Select a microhack from the list and open scoped dashboard
    When I open the organizer dashboard
    And I select microhack "mh-open-1"
    Then I should open the dashboard for "mh-open-1" only
    And subsequent operations should stay scoped to "mh-open-1"

  # ---------------------------------------------------------------------------
  # Selected microhack monitoring and lifecycle — DASH-015 through DASH-018
  # ---------------------------------------------------------------------------

  @DASH-015 @DASH-016
  Scenario: Selected microhack dashboard shows tree and status
    Given I selected microhack "mh-open-1"
    When I open the selected microhack dashboard
    Then I should see the hierarchy tree of teams and hackers
    And I should see total challenges as "8"
    And Team Alpha should display challenge progress "3 / 8"
    And Team Alpha should display elapsed time

  @DASH-017
  Scenario: Start and stop hack lifecycle from selected microhack dashboard
    Given I selected microhack "mh-open-1"
    When I click "Start Hack"
    Then the API should set lifecycle state for "mh-open-1" to "started"
    When I click "Stop Hack"
    Then the API should set lifecycle state for "mh-open-1" to "completed"

  @DASH-018
  Scenario: Selected microhack dashboard does not auto-refresh
    Given I selected microhack "mh-open-1"
    When I open the selected microhack dashboard
    And Team Alpha challenge step changes externally
    Then the page should still show the original values until I manually reload

  # ---------------------------------------------------------------------------
  # Per-team challenge operations (selected microhack) — DASH-010 through DASH-014
  # ---------------------------------------------------------------------------

  @DASH-010
  Scenario: Advance a single team challenge in selected microhack
    Given I selected microhack "mh-open-1"
    When I click the Advance button for "Team Alpha"
    Then the API should move "Team Alpha" from step 3 to step 4 in "mh-open-1"

  @DASH-011
  Scenario: Revert a single team challenge in selected microhack
    Given I selected microhack "mh-open-1"
    When I click the Revert button for "Team Alpha"
    Then the API should move "Team Alpha" from step 3 to step 2 in "mh-open-1"

  @DASH-012
  Scenario: Reset a single team challenge in selected microhack
    Given I selected microhack "mh-open-1"
    When I click the Reset button for "Team Alpha"
    Then the API should reset "Team Alpha" to step 1 in "mh-open-1"

  @DASH-013
  Scenario: Advance fails on last challenge
    Given I selected microhack "mh-open-1"
    When I click the Advance button for "Team Gamma"
    Then the API should return a 400 error with message "Cannot advance past the last challenge"

  @DASH-014
  Scenario: Revert fails on first challenge
    Given I selected microhack "mh-open-1"
    When I click the Revert button for "Team Beta"
    Then the API should return a 400 error with message "Cannot revert before the first challenge"

  # ---------------------------------------------------------------------------
  # Bulk challenge operations (selected microhack) — DASH-020 through DASH-024
  # ---------------------------------------------------------------------------

  @bulk @DASH-020 @DASH-021 @DASH-022
  Scenario Outline: Bulk challenge action on selected microhack
    Given I selected microhack "mh-open-1"
    When I click the "<action>" bulk challenge button
    Then the API should apply "<action>" to teams in "mh-open-1" only

    Examples:
      | action      |
      | Advance All |
      | Revert All  |
      | Reset All   |

  @bulk @DASH-023 @DASH-024
  Scenario: Bulk challenge action returns per-team summary with partial failure
    Given I selected microhack "mh-open-1"
    When I click the "Advance All" bulk challenge button
    Then the API should process each team independently
    And the bulk result should include team-level success or error entries
    And I should see a summary of successes and failures

  # ---------------------------------------------------------------------------
  # Per-team timer operations (selected microhack) — DASH-030 through DASH-034
  # ---------------------------------------------------------------------------

  @DASH-030
  Scenario: Start timer for a single team
    Given I selected microhack "mh-open-1"
    When I click the Start timer button for "Team Beta"
    Then the API should start the timer for "Team Beta" in "mh-open-1"

  @DASH-031
  Scenario: Stop timer for a single team
    Given I selected microhack "mh-open-1"
    When I click the Stop timer button for "Team Alpha"
    Then the API should stop the timer for "Team Alpha" in "mh-open-1"

  @DASH-032
  Scenario: Reset timer for a single team
    Given I selected microhack "mh-open-1"
    When I click the Reset timer button for "Team Alpha"
    Then the API should reset and stop the timer for "Team Alpha" in "mh-open-1"

  @DASH-033
  Scenario: Start timer button disabled when already running
    Given I selected microhack "mh-open-1"
    When I open the selected microhack dashboard
    Then the Start timer button for "Team Alpha" should be disabled

  @DASH-034
  Scenario: Stop timer button disabled when not running
    Given I selected microhack "mh-open-1"
    When I open the selected microhack dashboard
    Then the Stop timer button for "Team Beta" should be disabled

  # ---------------------------------------------------------------------------
  # Bulk timer operations (selected microhack) — DASH-040 through DASH-044
  # ---------------------------------------------------------------------------

  @bulk @DASH-040 @DASH-041 @DASH-042
  Scenario Outline: Bulk timer action on selected microhack
    Given I selected microhack "mh-open-1"
    When I click the "<action>" bulk timer button
    Then the API should apply "<action>" timer action to teams in "mh-open-1" only

    Examples:
      | action            |
      | Start All Timers  |
      | Stop All Timers   |
      | Reset All Timers  |

  @bulk @DASH-043 @DASH-044
  Scenario: Bulk timer action returns per-team summary with partial failure
    Given I selected microhack "mh-open-1"
    When I click the "Start All Timers" bulk timer button
    Then the API should process each team independently
    And the bulk timer result should include team-level success or error entries
    And I should see a summary of successes and failures

  # ---------------------------------------------------------------------------
  # API and navigation access control — DASH-050 through DASH-053
  # ---------------------------------------------------------------------------

  @DASH-050
  Scenario: Dashboard APIs require organizer role
    Given I am logged in as a user with the "organizer" role
    When I request GET /api/dashboard/microhacks
    Then the API should return a 200 response

  @DASH-051
  Scenario Outline: Non-organizer receives 403
    Given I am logged in as a user with the "<role>" role
    When I request <method> <endpoint>
    Then the API should return a 403 Forbidden response

    Examples:
      | role        | method | endpoint                                                     |
      | participant | GET    | /api/dashboard/microhacks                                   |
      | coach       | POST   | /api/dashboard/microhacks/mh-open-1/lifecycle              |
      | participant | POST   | /api/dashboard/microhacks/mh-open-1/challenge/bulk         |
      | coach       | POST   | /api/dashboard/microhacks/mh-open-1/timer/bulk             |

  @DASH-052
  Scenario Outline: Unauthenticated requests receive 401
    Given I am not authenticated
    When I request <method> <endpoint>
    Then the API should return a 401 Unauthorized response

    Examples:
      | method | endpoint                                                     |
      | GET    | /api/dashboard/microhacks                                   |
      | GET    | /api/dashboard/microhacks/mh-open-1/teams                  |
      | POST   | /api/dashboard/microhacks/mh-open-1/lifecycle              |
      | POST   | /api/dashboard/microhacks/mh-open-1/challenge/bulk         |
      | POST   | /api/dashboard/microhacks/mh-open-1/timer/bulk             |

  @DASH-053
  Scenario: Organizer navigation link visibility
    Given I am logged in as a user with the "organizer" role
    When I view the application navigation
    Then I should see a link to the organizer dashboard
    Given I am logged in as a user with the "participant" role
    When I view the application navigation
    Then I should not see a link to the organizer dashboard
