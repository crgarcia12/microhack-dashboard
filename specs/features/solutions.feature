@solutions
Feature: Solution Viewing (Coach Only)
  Coaches can browse expected answers for each challenge.
  Solutions are loaded from Markdown files at startup and served read-only.
  Participants and organizers must never access solutions.

  Background:
    Given the application has loaded solution files from "hackcontent/solutions/"

  # ── Content Loading ──────────────────────────────────────────────

  @coach @SOL-001
  Scenario: Solution files are loaded from the correct directory at startup
    Given the "hackcontent/solutions/" directory contains files "solution-001.md", "solution-002.md", "solution-003.md"
    When the application starts
    Then the API has loaded 3 solutions from files matching the pattern "solution-###.md"

  @coach @SOL-002
  Scenario: Solutions are sorted by numeric suffix in ascending order
    Given solutions "solution-003.md", "solution-001.md", "solution-002.md" are loaded
    When a coach requests "GET /api/solutions"
    Then the solutions list is ordered as numbers 1, 2, 3

  @coach @SOL-003
  Scenario: Each solution is associated with its corresponding challenge by number
    Given "solution-001.md" and "challenge-001.md" both exist
    When a coach requests "GET /api/solutions/1"
    Then the solution number is 1 corresponding to challenge 1

  @coach @SOL-004
  Scenario: Empty solutions directory returns empty list
    Given the "hackcontent/solutions/" directory contains no solution files
    When a coach requests "GET /api/solutions"
    Then the response status is 200
    And the response body contains "solutions" as an empty array and "totalCount" as 0

  @coach @SOL-004
  Scenario: Frontend shows empty-state message when no solutions loaded
    Given no solution files are loaded
    When a coach visits the Solutions page
    Then the page displays "No solutions loaded — add Markdown files to hackcontent/solutions/"

  @coach @SOL-005
  Scenario: Solutions are loaded once at startup and not refreshed
    Given solutions have been loaded at startup
    When a new file "solution-099.md" is added to "hackcontent/solutions/" after startup
    And a coach requests "GET /api/solutions"
    Then solution 99 is not included in the response

  # ── Content Rendering ────────────────────────────────────────────

  @coach @SOL-006
  Scenario: API returns raw Markdown and frontend renders it as HTML
    Given solution 1 has Markdown content "# Title\n\nSome **bold** text"
    When a coach requests "GET /api/solutions/1"
    Then the response "content" field contains the raw Markdown string
    And the frontend renders it as HTML with a heading and bold text

  @coach @SOL-007
  Scenario: Markdown headings render with correct hierarchy
    Given solution 1 contains headings H1 through H6
    When a coach views solution 1
    Then each heading renders at the correct HTML heading level

  @coach @SOL-007
  Scenario: Fenced code blocks render with syntax highlighting
    Given solution 1 contains a fenced code block tagged as "python"
    When a coach views solution 1
    Then the code block renders with syntax highlighting for Python

  @coach @SOL-007
  Scenario: Ordered and unordered lists render correctly
    Given solution 1 contains an ordered list and an unordered list
    When a coach views solution 1
    Then both list types render with correct HTML list elements

  @coach @SOL-007
  Scenario: Pipe-delimited tables render correctly
    Given solution 1 contains a pipe-delimited Markdown table
    When a coach views solution 1
    Then the table renders as an HTML table with correct rows and columns

  @coach @SOL-008
  Scenario: Inline images resolve to the solutions media endpoint
    Given solution 1 contains Markdown image "![diagram](media/0010.png)"
    When a coach views solution 1
    Then the image src resolves to "/api/solutions/media/0010.png"

  @coach @SOL-009
  Scenario: Solution rendering uses the same component as challenges
    Given the Markdown rendering component is shared between Challenges and Solutions
    When a coach views a solution with the same Markdown as a challenge
    Then the rendered output is visually identical

  # ── Access Control ───────────────────────────────────────────────

  @security @coach @SOL-010
  Scenario: Coach can access the solutions list endpoint
    Given a user is authenticated with the "coach" role
    When they request "GET /api/solutions"
    Then the response status is 200
    And the response contains a list of solutions

  @security @coach @SOL-010
  Scenario: Coach can access a single solution endpoint
    Given a user is authenticated with the "coach" role
    And solution 2 exists
    When they request "GET /api/solutions/2"
    Then the response status is 200
    And the response contains the solution content

  @security @coach @SOL-010
  Scenario: Coach can access the solutions media endpoint
    Given a user is authenticated with the "coach" role
    And media file "0010.png" exists in "hackcontent/solutions/media/"
    When they request "GET /api/solutions/media/0010.png"
    Then the response status is 200
    And the response content type is "image/png"

  @security @SOL-011
  Scenario: Participant is denied access to solutions list
    Given a user is authenticated with the "participant" role
    When they request "GET /api/solutions"
    Then the response status is 403
    And the response body is { "error": "Forbidden" }

  @security @SOL-011
  Scenario: Participant is denied access to a single solution
    Given a user is authenticated with the "participant" role
    When they request "GET /api/solutions/1"
    Then the response status is 403
    And the response body is { "error": "Forbidden" }

  @security @SOL-011
  Scenario: Participant is denied access to solution media
    Given a user is authenticated with the "participant" role
    When they request "GET /api/solutions/media/0010.png"
    Then the response status is 403
    And the response body is { "error": "Forbidden" }

  @security @SOL-012
  Scenario: Organizer is denied access to solutions list
    Given a user is authenticated with the "organizer" role
    When they request "GET /api/solutions"
    Then the response status is 403
    And the response body is { "error": "Forbidden" }

  @security @SOL-012
  Scenario: Organizer is denied access to a single solution
    Given a user is authenticated with the "organizer" role
    When they request "GET /api/solutions/1"
    Then the response status is 403
    And the response body is { "error": "Forbidden" }

  @security @SOL-013
  Scenario: Solutions navigation link is hidden for participants
    Given a user is authenticated with the "participant" role
    When the navigation menu renders
    Then the "Solutions" link is not visible

  @security @SOL-013
  Scenario: Solutions navigation link is hidden for organizers
    Given a user is authenticated with the "organizer" role
    When the navigation menu renders
    Then the "Solutions" link is not visible

  @security @SOL-013
  Scenario: Solutions navigation link is visible for coaches
    Given a user is authenticated with the "coach" role
    When the navigation menu renders
    Then the "Solutions" link is visible

  @security @SOL-014
  Scenario: Non-coach user navigating directly to /solutions is redirected
    Given a user is authenticated with the "participant" role
    When they navigate directly to "/solutions"
    Then they are redirected to "/challenges"

  @security @SOL-014
  Scenario: Organizer navigating directly to /solutions is redirected
    Given a user is authenticated with the "organizer" role
    When they navigate directly to "/solutions"
    Then they are redirected to "/challenges"

  # ── Approval Controls ────────────────────────────────────────────

  @coach @SOL-015
  Scenario: Solutions page displays Approve, Revert, and Reset controls
    Given a coach is viewing the Solutions page
    Then the Approve button is visible
    And the Revert button is visible
    And the Reset button is visible

  @coach @SOL-016
  Scenario: Approve advances the team to the next challenge step
    Given a coach is viewing the Solutions page
    And the team's current step is 2
    When the coach clicks the Approve button
    Then the team's current step becomes 3

  @coach @SOL-016
  Scenario: Revert decrements the team's current step by one
    Given a coach is viewing the Solutions page
    And the team's current step is 3
    When the coach clicks the Revert button
    Then the team's current step becomes 2

  @coach @SOL-016
  Scenario: Revert does not go below step 1
    Given a coach is viewing the Solutions page
    And the team's current step is 1
    When the coach clicks the Revert button
    Then the team's current step remains 1

  @coach @SOL-016
  Scenario: Reset returns the team to step 1 and clears all times
    Given a coach is viewing the Solutions page
    And the team's current step is 4
    When the coach clicks the Reset button
    Then the team's current step becomes 1
    And all recorded times are cleared

  @coach @SOL-017
  Scenario: Approve is triggered via Ctrl+Enter keyboard shortcut
    Given a coach is viewing the Solutions page
    And the team's current step is 2
    When the coach presses "Ctrl+Enter"
    Then the team's current step becomes 3

  @coach @SOL-018
  Scenario: Approval changes are broadcast via SignalR to team members
    Given a coach is viewing the Solutions page
    And a participant is connected via SignalR
    When the coach clicks the Approve button
    Then the participant receives a SignalR event reflecting the updated step

  # ── Navigation ───────────────────────────────────────────────────

  @coach @SOL-019
  Scenario: Solutions page shows a navigation list of all solutions
    Given 6 solutions are loaded
    When a coach visits the Solutions page
    Then a sidebar lists all 6 solutions with sequence numbers and titles

  @coach @SOL-020
  Scenario: Currently viewed solution is highlighted in the navigation list
    Given a coach is viewing solution 3
    Then solution 3 is visually highlighted in the navigation list

  @coach @SOL-021
  Scenario: Team's current step is indicated in the solution navigation list
    Given the team's current step is 4
    When a coach views the Solutions page
    Then solution 4 has a marker indicating it is the team's current step

  # ── Browsing ─────────────────────────────────────────────────────

  @coach @SOL-019
  Scenario: Coach selects a solution from the list and sees its content
    Given 6 solutions are loaded
    When a coach selects solution 3 from the navigation list
    Then the full Markdown content of solution 3 is rendered in the content area

  @coach @SOL-001 @SOL-002
  Scenario: Solutions list shows total count of available solutions
    Given 6 solutions are loaded
    When a coach requests "GET /api/solutions"
    Then the response "totalCount" is 6

  @coach
  Scenario: Coach can browse solutions independently of team's current step
    Given the team's current step is 2
    And 6 solutions are loaded
    When a coach selects solution 5 from the navigation list
    Then the content of solution 5 is displayed regardless of the team being on step 2

  # ── Edge Cases ───────────────────────────────────────────────────

  @coach
  Scenario: Request for a non-existent solution returns 404
    Given 6 solutions are loaded
    When a coach requests "GET /api/solutions/99"
    Then the response status is 404
    And the response body contains "Solution not found" and number 99

  @coach
  Scenario: Invalid solution number returns 400
    When a coach requests "GET /api/solutions/abc"
    Then the response status is 400
    And the response body contains "Invalid solution number"

  @coach
  Scenario: Empty solution file is loaded with empty content and filename as title
    Given "solution-003.md" exists but is 0 bytes
    When a coach requests "GET /api/solutions/3"
    Then the response "content" is an empty string
    And the response "title" is "solution-003.md"

  @coach
  Scenario: Solution file without H1 heading falls back to filename as title
    Given "solution-002.md" contains no H1 heading
    When a coach requests "GET /api/solutions/2"
    Then the response "title" is "solution-002.md"

  @coach
  Scenario: Requesting non-existent media file returns 404
    When a coach requests "GET /api/solutions/media/missing.png"
    Then the response status is 404
    And the response body contains "Media file not found"

  @security @coach
  Scenario: Path traversal in media filename returns 400
    When a coach requests "GET /api/solutions/media/../../etc/passwd"
    Then the response status is 400
    And the response body contains "Invalid filename"

  @security @coach
  Scenario: Media file with disallowed extension returns 400
    When a coach requests "GET /api/solutions/media/script.exe"
    Then the response status is 400
    And the response body contains "Unsupported file type"

  @coach
  Scenario: Solution count differs from challenge count without error
    Given 6 solutions and 8 challenges are loaded
    When a coach requests "GET /api/solutions"
    Then the response "totalCount" is 6 with no error

  @coach
  Scenario: Media endpoint returns correct cache headers
    Given media file "0010.png" exists
    When a coach requests "GET /api/solutions/media/0010.png"
    Then the response includes header "Cache-Control" with value "public, max-age=86400"

  @security
  Scenario: Unauthenticated request to solutions endpoint returns 401
    Given no session cookie is present
    When they request "GET /api/solutions"
    Then the response status is 401
    And the response body is { "error": "Unauthorized" }

  @coach
  Scenario: Response includes the requesting coach's team current step
    Given a coach belongs to a team whose current step is 3
    When the coach requests "GET /api/solutions"
    Then the response "currentStep" is 3
