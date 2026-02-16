@challenges
Feature: Challenge Progression
  As a participant or coach,
  I want challenges to be presented sequentially with coach-controlled progression,
  so that teams move through hackathon tasks at the right pace.

  # -------------------------------------------------------------------
  # Background: common setup for most scenarios
  # -------------------------------------------------------------------

  Background:
    Given the API has loaded 6 challenge files from "hackcontent/challenges/"
    And a team "team-alpha" exists with currentStep 2
    And I am logged in as a participant on team "team-alpha"

  # ===================================================================
  # Challenge Loading (CHAL-001 – CHAL-006)
  # ===================================================================

  @smoke @CHAL-001 @CHAL-002 @CHAL-003
  Scenario: Challenges are loaded from disk in numeric order at startup
    Given the "hackcontent/challenges/" directory contains files:
      | filename          |
      | challenge-003.md  |
      | challenge-001.md  |
      | challenge-002.md  |
    When the API starts up
    Then 3 challenges are loaded in memory
    And the challenge sequence is 1, 2, 3

  @CHAL-004
  Scenario: Non-matching files are ignored during loading
    Given the "hackcontent/challenges/" directory contains files:
      | filename          |
      | challenge-001.md  |
      | README.md         |
      | notes.txt         |
      | challenge-02.md   |
    When the API starts up
    Then 1 challenge is loaded in memory

  @CHAL-005
  Scenario: Empty challenges directory activates empty state
    Given the "hackcontent/challenges/" directory contains no matching files
    When the API starts up
    Then the total challenge count is 0

  @CHAL-006
  Scenario: Challenge content is immutable after startup
    Given the API has started with 3 challenge files
    When a new file "challenge-004.md" is added to "hackcontent/challenges/"
    And I request GET "/api/challenges"
    Then the response contains 3 challenges

  # ===================================================================
  # Challenge Retrieval — List (CHAL-010 – CHAL-012, CHAL-016, CHAL-017)
  # ===================================================================

  @smoke @CHAL-010 @CHAL-017
  Scenario: Participant sees challenge list with correct statuses
    When I request GET "/api/challenges"
    Then the response status is 200
    And the response is a JSON array of 6 challenges
    And challenge 1 has status "completed"
    And challenge 2 has status "current"
    And challenge 3 has status "locked"

  @CHAL-011
  Scenario: Challenge title is extracted from first heading
    Given challenge file "challenge-001.md" starts with "# Azure Migration MicroHack"
    When I request GET "/api/challenges"
    Then challenge 1 has title "Azure Migration MicroHack"

  @CHAL-011
  Scenario: Challenge with no heading gets default title
    Given challenge file "challenge-001.md" contains no "# heading" line
    When I request GET "/api/challenges"
    Then challenge 1 has title "Challenge 1"

  @CHAL-012
  Scenario: Locked challenges have null title
    When I request GET "/api/challenges"
    Then challenge 3 has title null
    And challenge 4 has title null
    And challenge 5 has title null
    And challenge 6 has title null

  @CHAL-016
  Scenario: Unauthenticated request to challenge list returns 401
    Given I am not authenticated
    When I request GET "/api/challenges"
    Then the response status is 401
    And the response body contains error "Authentication required"

  # ===================================================================
  # Challenge Retrieval — Single (CHAL-013 – CHAL-015, CHAL-016)
  # ===================================================================

  @smoke @CHAL-013
  Scenario: Participant retrieves current challenge content
    When I request GET "/api/challenges/2"
    Then the response status is 200
    And the response contains "challengeNumber" equal to 2
    And the response contains a non-empty "contentHtml" field
    And the response contains a "title" field

  @CHAL-013
  Scenario: Participant retrieves a completed challenge
    When I request GET "/api/challenges/1"
    Then the response status is 200
    And the response contains "challengeNumber" equal to 1
    And the response contains a non-empty "contentHtml" field

  @CHAL-014
  Scenario: Requesting a locked challenge returns 403
    When I request GET "/api/challenges/3"
    Then the response status is 403
    And the response body contains error "Challenge is locked"

  @CHAL-015
  Scenario: Requesting a non-existent challenge returns 404
    When I request GET "/api/challenges/99"
    Then the response status is 404
    And the response body contains error "Challenge not found"

  @CHAL-016
  Scenario: Unauthenticated request to single challenge returns 401
    Given I am not authenticated
    When I request GET "/api/challenges/1"
    Then the response status is 401

  # ===================================================================
  # Challenge Retrieval — Status computation (CHAL-017)
  # ===================================================================

  @CHAL-017
  Scenario Outline: Challenge status is computed from team currentStep
    Given a team "team-alpha" exists with currentStep <currentStep>
    When I request GET "/api/challenges"
    Then challenge <number> has status "<expectedStatus>"

    Examples:
      | currentStep | number | expectedStatus |
      | 3           | 1      | completed      |
      | 3           | 2      | completed      |
      | 3           | 3      | current        |
      | 3           | 4      | locked         |
      | 1           | 1      | current        |
      | 6           | 6      | current        |
      | 7           | 6      | completed      |

  # ===================================================================
  # Team Progress (CHAL-020 – CHAL-024)
  # ===================================================================

  @smoke @CHAL-020 @CHAL-021
  Scenario: Participant retrieves team progress
    When I request GET "/api/teams/progress"
    Then the response status is 200
    And the response contains "teamId" equal to "team-alpha"
    And the response contains "currentStep" equal to 2
    And the response contains "totalChallenges" equal to 6
    And the response contains "completedChallenges" equal to 1
    And the response contains "completed" equal to false

  @CHAL-022
  Scenario: Team that completed all challenges shows completed true
    Given a team "team-alpha" exists with currentStep 7
    When I request GET "/api/teams/progress"
    Then the response contains "completed" equal to true
    And the response contains "completedChallenges" equal to 6

  @CHAL-023
  Scenario: Team progress when no challenges are loaded
    Given the API has loaded 0 challenge files
    And a team "team-alpha" exists
    When I request GET "/api/teams/progress"
    Then the response contains "currentStep" equal to 0
    And the response contains "totalChallenges" equal to 0
    And the response contains "completed" equal to false

  @CHAL-024
  Scenario: Unauthenticated request to team progress returns 401
    Given I am not authenticated
    When I request GET "/api/teams/progress"
    Then the response status is 401
    And the response body contains error "Authentication required"

  # ===================================================================
  # Coach Actions — Approve (CHAL-030 – CHAL-032, CHAL-039 – CHAL-042)
  # ===================================================================

  @smoke @coach @CHAL-030 @CHAL-041
  Scenario: Coach approves current challenge
    Given I am logged in as a coach on team "team-alpha"
    When I request POST "/api/teams/progress/approve"
    Then the response status is 200
    And the response contains "currentStep" equal to 3
    And the team progress file on disk has currentStep 3

  @coach @CHAL-031
  Scenario: Coach approve when all challenges completed returns 409
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 7
    When I request POST "/api/teams/progress/approve"
    Then the response status is 409
    And the response body contains error "All challenges already completed"

  @coach @CHAL-032
  Scenario: Coach approve with no challenges loaded returns 409
    Given the API has loaded 0 challenge files
    And I am logged in as a coach on team "team-alpha"
    When I request POST "/api/teams/progress/approve"
    Then the response status is 409
    And the response body contains error "No challenges loaded"

  # ===================================================================
  # Coach Actions — Revert (CHAL-033 – CHAL-035)
  # ===================================================================

  @coach @CHAL-033 @CHAL-041
  Scenario: Coach reverts to previous challenge
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 3
    When I request POST "/api/teams/progress/revert"
    Then the response status is 200
    And the response contains "currentStep" equal to 2
    And the team progress file on disk has currentStep 2

  @coach @CHAL-034
  Scenario: Coach revert at challenge 1 returns 409
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 1
    When I request POST "/api/teams/progress/revert"
    Then the response status is 409
    And the response body contains error "Already at first challenge"

  @coach @CHAL-035
  Scenario: Coach revert with no challenges loaded returns 409
    Given the API has loaded 0 challenge files
    And I am logged in as a coach on team "team-alpha"
    When I request POST "/api/teams/progress/revert"
    Then the response status is 409
    And the response body contains error "No challenges loaded"

  # ===================================================================
  # Coach Actions — Reset (CHAL-036 – CHAL-038)
  # ===================================================================

  @coach @CHAL-036 @CHAL-041
  Scenario: Coach resets team to challenge 1
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 5
    When I request POST "/api/teams/progress/reset"
    Then the response status is 200
    And the response contains "currentStep" equal to 1
    And the team progress file on disk has currentStep 1

  @coach @CHAL-037
  Scenario: Coach reset when already at challenge 1 is idempotent
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 1
    When I request POST "/api/teams/progress/reset"
    Then the response status is 200
    And the response contains "currentStep" equal to 1

  @coach @CHAL-038
  Scenario: Coach reset with no challenges loaded returns 409
    Given the API has loaded 0 challenge files
    And I am logged in as a coach on team "team-alpha"
    When I request POST "/api/teams/progress/reset"
    Then the response status is 409
    And the response body contains error "No challenges loaded"

  # ===================================================================
  # Coach Actions — Authorization (CHAL-039, CHAL-040)
  # ===================================================================

  @coach @CHAL-039
  Scenario Outline: Participant cannot call coach endpoints
    When I request POST "/api/teams/progress/<action>"
    Then the response status is 403
    And the response body contains error "Insufficient permissions"

    Examples:
      | action  |
      | approve |
      | revert  |
      | reset   |

  @coach @CHAL-039
  Scenario: Organizer can approve challenges
    Given I am logged in as an organizer on team "team-alpha"
    When I request POST "/api/teams/progress/approve"
    Then the response status is 200
    And the response contains "currentStep" equal to 3

  @coach @CHAL-040
  Scenario Outline: Unauthenticated requests to coach endpoints return 401
    Given I am not authenticated
    When I request POST "/api/teams/progress/<action>"
    Then the response status is 401

    Examples:
      | action  |
      | approve |
      | revert  |
      | reset   |

  # ===================================================================
  # SignalR Real-Time Updates (CHAL-042, CHAL-E13)
  # ===================================================================

  @realtime @CHAL-042
  Scenario: Coach approve broadcasts progressUpdated via SignalR
    Given I am logged in as a coach on team "team-alpha"
    And a participant on team "team-alpha" is connected to the SignalR hub "/hubs/progress"
    When I request POST "/api/teams/progress/approve"
    Then the participant receives a "progressUpdated" event within 2 seconds
    And the event payload contains "currentStep" equal to 3

  @realtime @CHAL-042
  Scenario: Coach revert broadcasts progressUpdated via SignalR
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 3
    And a participant on team "team-alpha" is connected to the SignalR hub "/hubs/progress"
    When I request POST "/api/teams/progress/revert"
    Then the participant receives a "progressUpdated" event within 2 seconds
    And the event payload contains "currentStep" equal to 2

  @realtime @CHAL-042
  Scenario: Coach reset broadcasts progressUpdated via SignalR
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 4
    And a participant on team "team-alpha" is connected to the SignalR hub "/hubs/progress"
    When I request POST "/api/teams/progress/reset"
    Then the participant receives a "progressUpdated" event within 2 seconds
    And the event payload contains "currentStep" equal to 1

  @realtime @CHAL-042
  Scenario: SignalR events are scoped to the team group
    Given I am logged in as a coach on team "team-alpha"
    And a participant on team "team-beta" is connected to the SignalR hub "/hubs/progress"
    When I request POST "/api/teams/progress/approve"
    Then the participant on team "team-beta" does not receive a "progressUpdated" event

  # ===================================================================
  # Polling Fallback (CHAL-E13)
  # ===================================================================

  @realtime @CHAL-E13
  Scenario: Client switches to polling when SignalR disconnects
    Given a participant on team "team-alpha" is connected to the SignalR hub "/hubs/progress"
    When the SignalR connection drops
    Then the client polls GET "/api/teams/progress" every 5 seconds

  @realtime @CHAL-E13
  Scenario: Client stops polling when SignalR reconnects
    Given a participant on team "team-alpha" has fallen back to polling
    When the SignalR connection is re-established
    Then the client fetches the latest progress once
    And the client stops polling

  # ===================================================================
  # Markdown Rendering (CHAL-050 – CHAL-053)
  # ===================================================================

  @CHAL-050
  Scenario: Challenge content renders rich Markdown elements
    Given challenge 2 Markdown contains headings, code blocks, images, lists, tables, bold, italic, inline code, and links
    When I request GET "/api/challenges/2"
    Then the "contentHtml" contains "<h1>" tags for headings
    And the "contentHtml" contains "<pre>" tags for code blocks
    And the "contentHtml" contains "<img" tags for images
    And the "contentHtml" contains "<ul>" or "<ol>" tags for lists
    And the "contentHtml" contains "<table>" tags for tables

  @CHAL-051
  Scenario: Fenced code blocks have syntax highlighting
    Given challenge 2 Markdown contains a fenced code block with language "powershell"
    When I request GET "/api/challenges/2"
    Then the "contentHtml" contains a code block with language class "powershell"

  @CHAL-051
  Scenario: Fenced code blocks without language render as plain preformatted text
    Given challenge 2 Markdown contains a fenced code block with no language specified
    When I request GET "/api/challenges/2"
    Then the "contentHtml" contains a "<pre>" block without a language class

  @CHAL-052
  Scenario: Image paths are served via media endpoint
    When I request GET "/api/challenges/media/diagram.png"
    Then the response status is 200
    And the response content type is an image type

  @CHAL-052
  Scenario: Non-existent media file returns 404
    When I request GET "/api/challenges/media/nonexistent.png"
    Then the response status is 404
    And the response body contains error "File not found"

  @CHAL-053
  Scenario: Malformed Markdown renders best-effort HTML
    Given challenge 2 Markdown contains unclosed fences and broken table syntax
    When I request GET "/api/challenges/2"
    Then the response status is 200
    And the "contentHtml" is not empty

  # ===================================================================
  # Progress Bar (US-CHAL-04)
  # ===================================================================

  @smoke @CHAL-020 @CHAL-021
  Scenario Outline: Progress bar percentage is computed correctly
    Given a team "team-alpha" exists with currentStep <currentStep>
    And the API has loaded <total> challenge files
    When I request GET "/api/teams/progress"
    Then the progress percentage is <percentage>%

    Examples:
      | currentStep | total | percentage |
      | 1           | 6     | 0          |
      | 2           | 6     | 16         |
      | 4           | 6     | 50         |
      | 7           | 6     | 100        |

  # ===================================================================
  # Celebration Screen (CHAL-022, US-CHAL-05)
  # ===================================================================

  @smoke @CHAL-022
  Scenario: Celebration screen shown when all challenges completed
    Given a team "team-alpha" exists with currentStep 7
    When I request GET "/api/teams/progress"
    Then the response contains "completed" equal to true
    And the response contains "completedChallenges" equal to 6

  # ===================================================================
  # Empty State (CHAL-005, CHAL-023, CHAL-E01)
  # ===================================================================

  @CHAL-005 @CHAL-023 @CHAL-E01
  Scenario: Empty state when no challenges are loaded
    Given the API has loaded 0 challenge files
    When I request GET "/api/teams/progress"
    Then the response contains "totalChallenges" equal to 0
    And the response contains "currentStep" equal to 0
    And the response contains "completed" equal to false

  @CHAL-E01
  Scenario: Coach controls are disabled when no challenges loaded
    Given the API has loaded 0 challenge files
    And I am logged in as a coach on team "team-alpha"
    When I request POST "/api/teams/progress/approve"
    Then the response status is 409
    And the response body contains error "No challenges loaded"

  # ===================================================================
  # Edge Cases (CHAL-E01 – CHAL-E15)
  # ===================================================================

  @CHAL-E02
  Scenario: Coach approve past last challenge returns 409
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 7
    When I request POST "/api/teams/progress/approve"
    Then the response status is 409
    And the response body contains error "All challenges already completed"

  @CHAL-E03
  Scenario: Coach revert at first challenge returns 409
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 1
    When I request POST "/api/teams/progress/revert"
    Then the response status is 409
    And the response body contains error "Already at first challenge"

  @CHAL-E04
  Scenario: Coach reset at first challenge is idempotent
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 1
    When I request POST "/api/teams/progress/reset"
    Then the response status is 200

  @CHAL-E05
  Scenario: Malformed Markdown does not produce empty page
    Given challenge 1 Markdown contains unclosed fences and broken table syntax
    When I request GET "/api/challenges/1"
    Then the response status is 200
    And the "contentHtml" is not empty

  @CHAL-E06
  Scenario: Challenge file with no heading gets default title
    Given challenge file "challenge-001.md" contains no "# heading" line
    When I request GET "/api/challenges"
    Then challenge 1 has title "Challenge 1"

  @CHAL-E07
  Scenario: Gap in file numbering is transparent to users
    Given the "hackcontent/challenges/" directory contains files:
      | filename          |
      | challenge-001.md  |
      | challenge-002.md  |
      | challenge-004.md  |
    When the API starts up
    Then 3 challenges are loaded in memory
    And the challenge sequence is 1, 2, 3

  @CHAL-E08
  Scenario Outline: Participant calling coach endpoint gets 403
    When I request POST "/api/teams/progress/<action>"
    Then the response status is 403
    And the response body contains error "Insufficient permissions"

    Examples:
      | action  |
      | approve |
      | revert  |
      | reset   |

  @CHAL-E09
  Scenario Outline: Unauthenticated request to challenge endpoints returns 401
    Given I am not authenticated
    When I request <method> "<path>"
    Then the response status is 401

    Examples:
      | method | path                         |
      | GET    | /api/challenges              |
      | GET    | /api/challenges/1            |
      | GET    | /api/teams/progress          |
      | POST   | /api/teams/progress/approve  |
      | POST   | /api/teams/progress/revert   |
      | POST   | /api/teams/progress/reset    |

  @CHAL-E10
  Scenario: Team with no progress file defaults to currentStep 1
    Given team "team-new" has no progress file on disk
    And I am logged in as a participant on team "team-new"
    When I request GET "/api/teams/progress"
    Then the response contains "currentStep" equal to 1

  @CHAL-E11
  Scenario: Corrupted progress file is treated as currentStep 1
    Given team "team-alpha" has a corrupted progress file on disk
    When I request GET "/api/teams/progress"
    Then the response contains "currentStep" equal to 1

  @CHAL-E12
  Scenario: Progress file currentStep exceeding totalChallenges shows completed
    Given a team "team-alpha" exists with currentStep 10
    When I request GET "/api/teams/progress"
    Then the response contains "completed" equal to true

  @realtime @CHAL-E13
  Scenario: SignalR disconnect triggers polling fallback
    Given a participant on team "team-alpha" is connected to the SignalR hub "/hubs/progress"
    When the SignalR connection drops
    Then the client begins polling GET "/api/teams/progress" every 5 seconds
    And the UI shows a connection-status indicator

  @CHAL-E14
  Scenario: Two coaches acting simultaneously — last write wins
    Given I am logged in as a coach on team "team-alpha"
    And a team "team-alpha" exists with currentStep 3
    And another coach on team "team-alpha" sends POST "/api/teams/progress/approve"
    When I request POST "/api/teams/progress/revert"
    Then the response status is 200
    And the team progress reflects the last write

  @CHAL-E15
  Scenario: Empty challenge file renders with default title and empty content
    Given challenge file "challenge-001.md" is empty (0 bytes)
    When I request GET "/api/challenges"
    Then challenge 1 has title "Challenge 1"
    When I request GET "/api/challenges/1"
    Then the response status is 200
    And the "contentHtml" is empty or contains minimal HTML
