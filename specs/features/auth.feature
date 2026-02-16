@auth
Feature: Authentication & Sessions
  As a hackathon participant, coach, or event organizer
  I want to log in with my username and password
  So that I can access the features appropriate to my role

  Background:
    Given the following users are configured in "users.json":
      | username  | password   | role        | teamId |
      | alice     | hunter2    | participant | team1  |
      | bob       | coachpass  | coach       | team1  |
      | adminuser | adminpass  | techlead    |        |

  # ─── Login Page ──────────────────────────────────────────────

  @AUTH-001
  Scenario: Login page displays required fields
    When I navigate to the root URL "/"
    Then I should see a username input field
    And I should see a password input field
    And I should see a "Login" button

  @AUTH-002
  Scenario: Client-side validation rejects empty username
    Given I am on the login page
    When I leave the username field empty
    And I enter "hunter2" in the password field
    And I click the "Login" button
    Then I should see the message "Please fill in all fields"
    And no HTTP request should be sent to the server

  @AUTH-002
  Scenario: Client-side validation rejects empty password
    Given I am on the login page
    When I enter "alice" in the username field
    And I leave the password field empty
    And I click the "Login" button
    Then I should see the message "Please fill in all fields"
    And no HTTP request should be sent to the server

  @AUTH-002
  Scenario: Client-side validation rejects both fields empty
    Given I am on the login page
    When I leave the username field empty
    And I leave the password field empty
    And I click the "Login" button
    Then I should see the message "Please fill in all fields"
    And no HTTP request should be sent to the server

  # ─── Login API ───────────────────────────────────────────────

  @AUTH-003 @smoke
  Scenario: Successful login sends POST with credentials
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice", "password": "hunter2" }
      """
    Then the response status should be 200
    And the response body should contain "username" equal to "alice"
    And the response body should contain "role" equal to "participant"
    And the response body should contain "teamId" equal to "team1"

  @AUTH-004
  Scenario Outline: Username matching is case-insensitive
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "<username>", "password": "hunter2" }
      """
    Then the response status should be 200
    And the response body should contain "username" equal to "alice"

    Examples:
      | username |
      | alice    |
      | Alice    |
      | ALICE    |
      | aLiCe   |

  @AUTH-005 @error
  Scenario: Password matching is case-sensitive
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice", "password": "Hunter2" }
      """
    Then the response status should be 401
    And the response body should contain "error" equal to "Invalid username or password"

  @AUTH-006 @error
  Scenario: Login fails with non-existent username
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "nonexistent", "password": "hunter2" }
      """
    Then the response status should be 401
    And the response body should contain "error" equal to "Invalid username or password"

  @AUTH-006 @error
  Scenario: Login fails with wrong password
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice", "password": "wrongpass" }
      """
    Then the response status should be 401
    And the response body should contain "error" equal to "Invalid username or password"

  @AUTH-007
  Scenario: Successful login creates session with cryptographic session ID
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice", "password": "hunter2" }
      """
    Then the response status should be 200
    And the response should set a "sessionId" cookie
    And the "sessionId" cookie value should be at least 32 hex characters

  @AUTH-008
  Scenario: Session cookie has correct security attributes
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice", "password": "hunter2" }
      """
    Then the response should set a "sessionId" cookie
    And the "sessionId" cookie should have "HttpOnly" set to true
    And the "sessionId" cookie should have "SameSite" set to "Strict"
    And the "sessionId" cookie should have "Path" set to "/"

  @AUTH-009 @smoke
  Scenario: Participant is redirected to challenges after login
    Given a user "alice" with password "hunter2" and role "participant" exists
    When I submit login with username "alice" and password "hunter2"
    Then I should be redirected to "/challenges"

  @AUTH-009
  Scenario: Coach is redirected to challenges after login
    Given a user "bob" with password "coachpass" and role "coach" exists
    When I submit login with username "bob" and password "coachpass"
    Then I should be redirected to "/challenges"

  @AUTH-009
  Scenario: Techlead is redirected to dashboard after login
    Given a user "adminuser" with password "adminpass" and role "techlead" exists
    When I submit login with username "adminuser" and password "adminpass"
    Then I should be redirected to "/dashboard"

  @AUTH-010
  Scenario: Second login invalidates first session
    Given I am logged in as "alice" with password "hunter2"
    And I store my current session cookie as "firstSession"
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice", "password": "hunter2" }
      """
    Then the response status should be 200
    And the new session cookie should differ from "firstSession"
    When I send a GET request to "/api/auth/me" using "firstSession"
    Then the response status should be 401

  # ─── Logout ──────────────────────────────────────────────────

  @AUTH-011 @AUTH-012
  Scenario: Logout deletes session and clears cookie
    Given I am logged in as "alice" with password "hunter2"
    When I send a POST request to "/api/auth/logout"
    Then the response status should be 200
    And the response should set a "sessionId" cookie with "Max-Age" equal to "0"
    When I send a GET request to "/api/auth/me"
    Then the response status should be 401

  @AUTH-013
  Scenario: Frontend redirects to login page after logout
    Given I am logged in as "alice" with password "hunter2"
    When I click the "Logout" button
    Then I should be redirected to "/"

  @AUTH-014
  Scenario: Logout is idempotent when not logged in
    When I send a POST request to "/api/auth/logout" without a session cookie
    Then the response status should be 200

  @AUTH-014
  Scenario: Logout with invalid session still returns 200
    When I send a POST request to "/api/auth/logout" with an invalid session cookie
    Then the response status should be 200

  # ─── Session Validation & Identity Resolution ────────────────

  @AUTH-015 @error
  Scenario: Protected endpoint rejects request without session cookie
    When I send a GET request to "/api/auth/me" without a session cookie
    Then the response status should be 401
    And the response body should contain "error" equal to "Unauthorized"

  @AUTH-015 @error
  Scenario: Protected endpoint rejects request with invalid session cookie
    When I send a GET request to "/api/auth/me" with session cookie "invalidsessionid1234567890abcdef00"
    Then the response status should be 401
    And the response body should contain "error" equal to "Unauthorized"

  @AUTH-016
  Scenario: Session resolves to full user identity
    Given I am logged in as "alice" with password "hunter2"
    When I send a GET request to "/api/auth/me"
    Then the response status should be 200
    And the response body should contain "username" equal to "alice"
    And the response body should contain "role" equal to "participant"
    And the response body should contain "teamId" equal to "team1"

  @AUTH-016
  Scenario: Techlead identity has null teamId
    Given I am logged in as "adminuser" with password "adminpass"
    When I send a GET request to "/api/auth/me"
    Then the response status should be 200
    And the response body should contain "username" equal to "adminuser"
    And the response body should contain "role" equal to "techlead"
    And the response body should contain "teamId" equal to null

  @AUTH-017
  Scenario: Session remains valid without time-based expiry
    Given I am logged in as "alice" with password "hunter2"
    And I wait without making requests
    When I send a GET request to "/api/auth/me"
    Then the response status should be 200
    And the response body should contain "username" equal to "alice"

  @AUTH-018
  Scenario: GET /api/auth/me returns authenticated user identity
    Given I am logged in as "bob" with password "coachpass"
    When I send a GET request to "/api/auth/me"
    Then the response status should be 200
    And the response body should contain "username" equal to "bob"
    And the response body should contain "role" equal to "coach"
    And the response body should contain "teamId" equal to "team1"

  @AUTH-018 @error
  Scenario: GET /api/auth/me returns 401 when not authenticated
    When I send a GET request to "/api/auth/me" without a session cookie
    Then the response status should be 401
    And the response body should contain "error" equal to "Unauthorized"

  # ─── Role & Navigation ──────────────────────────────────────

  @AUTH-019 @error
  Scenario: Server rejects invalid role in users.json
    Given a "users.json" file with a user having role "admin"
    When the server starts
    Then the server should fail to start
    And the error log should contain "Invalid role 'admin' for user"

  @AUTH-020
  Scenario Outline: Navigation bar displays username and human-readable role label
    Given I am logged in as "<username>" with password "<password>"
    When I view the navigation bar
    Then I should see my username "<username>" in the navigation bar
    And I should see my role as "<roleLabel>" in the navigation bar

    Examples:
      | username  | password  | roleLabel   |
      | alice     | hunter2   | Participant |
      | bob       | coachpass | Coach       |
      | adminuser | adminpass | Tech Lead   |

  @AUTH-021
  Scenario: Participant sees only permitted navigation links
    Given I am logged in as "alice" with password "hunter2"
    When I view the navigation bar
    Then the navigation bar should contain "Challenges"
    And the navigation bar should contain "Credentials"
    And the navigation bar should contain "Timer"
    And the navigation bar should not contain "Dashboard"
    And the navigation bar should not contain "Solutions"

  @AUTH-021
  Scenario: Coach sees only permitted navigation links
    Given I am logged in as "bob" with password "coachpass"
    When I view the navigation bar
    Then the navigation bar should contain "Challenges"
    And the navigation bar should contain "Solutions"
    And the navigation bar should contain "Credentials"
    And the navigation bar should contain "Timer"
    And the navigation bar should not contain "Dashboard"

  @AUTH-021
  Scenario: Techlead sees all navigation links
    Given I am logged in as "adminuser" with password "adminpass"
    When I view the navigation bar
    Then the navigation bar should contain "Dashboard"
    And the navigation bar should contain "Challenges"
    And the navigation bar should contain "Solutions"
    And the navigation bar should contain "Credentials"
    And the navigation bar should contain "Timer"

  @AUTH-022
  Scenario: Participant redirected from unauthorized page
    Given I am logged in as "alice" with password "hunter2"
    When I navigate to "/dashboard"
    Then I should be redirected to "/challenges"

  @AUTH-022
  Scenario: Coach redirected from organizer-only page
    Given I am logged in as "bob" with password "coachpass"
    When I navigate to "/dashboard"
    Then I should be redirected to "/challenges"

  @AUTH-023
  Scenario: Unauthenticated user redirected to login page
    Given I am not logged in
    When I navigate to "/challenges"
    Then I should be redirected to "/"

  @AUTH-023
  Scenario: Unauthenticated user redirected from dashboard
    Given I am not logged in
    When I navigate to "/dashboard"
    Then I should be redirected to "/"

  # ─── Session Persistence Across Page Refresh ─────────────────

  @AUTH-009
  Scenario: Session persists across page refresh
    Given I am logged in as "alice" with password "hunter2"
    When I refresh the page
    Then I should remain logged in as "alice"
    And I should see role-appropriate content for "participant"

  @AUTH-017
  Scenario: Session invalidated after server restart
    Given I am logged in as "alice" with password "hunter2"
    And the server process has restarted
    When I send a GET request to "/api/auth/me"
    Then the response status should be 401

  # ─── Edge Cases ──────────────────────────────────────────────

  @error @EDGE-001
  Scenario: Server fails to start when users.json is missing
    Given the "users.json" file does not exist
    When the server starts
    Then the server should fail to start
    And the error log should contain "Users config file not found"

  @error @EDGE-002
  Scenario: All logins fail when users.json has empty users array
    Given a "users.json" file with an empty users array
    And the server is running
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice", "password": "hunter2" }
      """
    Then the response status should be 401

  @error @EDGE-003
  Scenario: Server fails to start with duplicate usernames
    Given a "users.json" file with users "Alice" and "alice"
    When the server starts
    Then the server should fail to start
    And the error log should contain "Duplicate username detected"

  @error @EDGE-004
  Scenario: Server fails to start with invalid role value
    Given a "users.json" file with a user having role "superadmin"
    When the server starts
    Then the server should fail to start
    And the error log should contain "Invalid role 'superadmin' for user"

  @error @EDGE-005
  Scenario: Server fails to start when participant has null teamId
    Given a "users.json" file with a participant having null teamId
    When the server starts
    Then the server should fail to start
    And the error log should contain "must have a teamId"

  @error @EDGE-006
  Scenario: Server fails to start when techlead has non-null teamId
    Given a "users.json" file with a techlead having teamId "team1"
    When the server starts
    Then the server should fail to start
    And the error log should contain "must not have a teamId"

  @error @EDGE-007
  Scenario: Valid-looking session ID not in store returns 401
    When I send a GET request to "/api/auth/me" with session cookie "aabbccdd00112233445566778899aabb"
    Then the response status should be 401
    And the response body should contain "error" equal to "Unauthorized"

  @error @EDGE-008
  Scenario Outline: Malformed session cookie returns 401 without crash
    When I send a GET request to "/api/auth/me" with session cookie "<cookie>"
    Then the response status should be 401
    And the response body should contain "error" equal to "Unauthorized"

    Examples:
      | cookie                                                             |
      |                                                                    |
      | not-hex-value!@#$%                                                 |
      | aabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899 |

  @error @EDGE-009
  Scenario: All sessions invalidated after server restart
    Given I am logged in as "alice" with password "hunter2"
    And I am logged in as "bob" with password "coachpass" in a second session
    When the server process restarts
    And I send a GET request to "/api/auth/me" as "alice"
    Then the response status should be 401
    When I send a GET request to "/api/auth/me" as "bob"
    Then the response status should be 401

  @error @EDGE-010
  Scenario: Concurrent login from second browser invalidates first
    Given I am logged in as "alice" with password "hunter2" on browser A
    When I log in as "alice" with password "hunter2" on browser B
    Then browser B should have a valid session
    When browser A sends a GET request to "/api/auth/me"
    Then the response status should be 401

  @error @EDGE-011
  Scenario: Server fails to start with malformed JSON in users.json
    Given a "users.json" file with malformed JSON content
    When the server starts
    Then the server should fail to start
    And the error log should contain "Failed to parse users config"

  @error @EDGE-012
  Scenario: Login with missing fields in request body returns 400
    When I send a POST request to "/api/auth/login" with body:
      """
      { "username": "alice" }
      """
    Then the response status should be 400
    And the response body should contain "error" equal to "Username and password are required"

  @error @EDGE-012
  Scenario: Login with empty request body returns 400
    When I send a POST request to "/api/auth/login" with body:
      """
      {}
      """
    Then the response status should be 400
    And the response body should contain "error" equal to "Username and password are required"

  @error @EDGE-012
  Scenario: Login with non-JSON request body returns 400
    When I send a POST request to "/api/auth/login" with body "not json"
    Then the response status should be 400
