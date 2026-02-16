@credentials
Feature: Credentials Display
  As a participant or coach
  I want to view my team's provisioned credentials grouped by category
  So that I can quickly find login details for the current challenge

  Background:
    Given the credentials file "hackcontent/credentials.json" exists with teams:
      | teamName | categories                                                        |
      | Team-01  | Azure: Portal Username=team01@contoso.com, Portal Password=secret |
      | Team-02  | VM Access: SSH Host=10.0.1.4, SSH Password=Cha11enge!             |
      | Team-03  |                                                                   |

  # --- US-CRED-1: View Team Credentials ---

  @smoke @CRED-010 @CRED-011 @CRED-014
  Scenario: Participant views their team's credentials via API
    Given I am authenticated as a participant on "Team-01"
    When I send a GET request to "/api/credentials"
    Then the response status should be 200
    And the response body should contain "teamName" equal to "Team-01"
    And the response body should contain a "categories" array with 1 entry
    And the first category should be named "Azure" with 2 credentials

  @CRED-002 @CRED-011
  Scenario: Team isolation — participant cannot see another team's credentials
    Given I am authenticated as a participant on "Team-01"
    When I send a GET request to "/api/credentials"
    Then the response status should be 200
    And the response body should not contain credentials for "Team-02"
    And the categories should not include "VM Access"

  @CRED-020 @CRED-021 @CRED-022 @CRED-023 @CRED-024
  Scenario: Credentials page renders grouped category cards
    Given I am authenticated as a participant on "Team-01"
    When I navigate to "/credentials"
    Then I should see a card with heading "Azure"
    And the "Azure" card should display "Portal Username" with value "team01@contoso.com"
    And the "Azure" card should display "Portal Password" with value "secret"

  @CRED-027
  Scenario: Credential values are selectable plain text
    Given I am authenticated as a participant on "Team-01"
    When I navigate to "/credentials"
    Then each credential value should be rendered as selectable text
    And no credential value should be masked or hidden

  # --- US-CRED-2: Empty State ---

  @CRED-015 @CRED-025
  Scenario: Team with no credentials sees empty state
    Given I am authenticated as a participant on "Team-03"
    When I send a GET request to "/api/credentials"
    Then the response status should be 200
    And the "categories" array should be empty
    When I navigate to "/credentials"
    Then I should see the message "No credentials have been provisioned for your team yet."

  @CRED-004
  Scenario: Missing credentials file shows empty state for all teams
    Given the credentials file "hackcontent/credentials.json" does not exist
    And I am authenticated as a participant on "Team-01"
    When I send a GET request to "/api/credentials"
    Then the response status should be 200
    And the "categories" array should be empty

  # --- US-CRED-3: Organizer Excluded ---

  @CRED-013
  Scenario: Organizer receives 403 from credentials API
    Given I am authenticated as an organizer
    When I send a GET request to "/api/credentials"
    Then the response status should be 403

  @CRED-020
  Scenario: Organizer does not see Credentials in navigation
    Given I am authenticated as an organizer
    When I view the navigation menu
    Then I should not see a "Credentials" link

  # --- Authentication ---

  @CRED-012
  Scenario: Unauthenticated request receives 401
    Given I am not authenticated
    When I send a GET request to "/api/credentials"
    Then the response status should be 401

  # --- Edge Cases ---

  @CRED-003
  Scenario: Credentials are cached — file read only at startup
    Given the credentials file is loaded at application startup
    And I am authenticated as a participant on "Team-01"
    When the credentials file is modified on disk after startup
    And I send a GET request to "/api/credentials"
    Then the response should reflect the original file contents

  @CRED-026
  Scenario: Loading state is displayed while credentials fetch is in progress
    Given I am authenticated as a participant on "Team-01"
    When I navigate to "/credentials" and the API response is delayed
    Then I should see a loading indicator

  Scenario: Category with empty credentials array is omitted
    Given the credentials file contains a category "Empty Group" with no credential entries for "Team-01"
    And I am authenticated as a participant on "Team-01"
    When I send a GET request to "/api/credentials"
    Then the response categories should not include "Empty Group"

  Scenario: Special characters in credential values are rendered safely
    Given the credentials file contains a credential with value "<script>alert('xss')</script>" for "Team-01"
    And I am authenticated as a participant on "Team-01"
    When I navigate to "/credentials"
    Then the value should be displayed as plain text, not rendered as HTML

  @CRED-020
  Scenario: Coach can access the credentials page
    Given I am authenticated as a coach on "Team-01"
    When I navigate to "/credentials"
    Then I should see a card with heading "Azure"
