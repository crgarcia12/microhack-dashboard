@lab-env @optional
Feature: Lab Environment Access
  Authenticated users can view remote desktop gateway URLs when configured.
  The feature is hidden entirely when no gateways are configured.

  Background:
    Given the user is authenticated

  # --- API: gateways configured ---

  @LAB-001 @LAB-004 @LAB-008
  Scenario: API returns enabled with multiple gateways
    Given the lab configuration contains gateways:
      | Name               | Url                              |
      | Gateway 1 (East US) | https://gw1.example.com/connect |
      | Gateway 2 (West US) | https://gw2.example.com/connect |
    When the user calls GET /api/lab
    Then the response status is 200
    And the response body has "enabled" equal to true
    And the "gateways" array contains 2 entries with matching name and url

  @LAB-001 @LAB-004 @LAB-008
  Scenario: API returns enabled with a single gateway
    Given the lab configuration contains gateways:
      | Name       | Url                              |
      | Main Gateway | https://gw.example.com/connect |
    When the user calls GET /api/lab
    Then the response status is 200
    And the response body has "enabled" equal to true
    And the "gateways" array contains 1 entries with matching name and url

  # --- API: no gateways configured ---

  @LAB-001 @LAB-003
  Scenario: API returns disabled when gateways array is empty
    Given the lab configuration has an empty gateways array
    When the user calls GET /api/lab
    Then the response status is 200
    And the response body has "enabled" equal to false
    And the "gateways" array contains 0 entries with matching name and url

  @LAB-001 @LAB-003
  Scenario: API returns disabled when gateways key is null
    Given the lab configuration has gateways set to null
    When the user calls GET /api/lab
    Then the response status is 200
    And the response body has "enabled" equal to false
    And the "gateways" array contains 0 entries with matching name and url

  @LAB-001 @LAB-003
  Scenario: API returns disabled when LabEnvironment section is missing
    Given the lab configuration section is missing entirely
    When the user calls GET /api/lab
    Then the response status is 200
    And the response body has "enabled" equal to false
    And the "gateways" array contains 0 entries with matching name and url

  # --- API: authentication ---

  @LAB-007
  Scenario: Unauthenticated request returns 401
    Given the user is not authenticated
    When the user calls GET /api/lab
    Then the response status is 401

  # --- API: URL validation ---

  @LAB-009
  Scenario: Invalid gateway URLs are excluded from the response
    Given the lab configuration contains gateways:
      | Name    | Url                              |
      | Valid   | https://gw.example.com/connect   |
      | Invalid | ftp://bad.example.com            |
      | Empty   |                                  |
    When the user calls GET /api/lab
    Then the response body has "enabled" equal to true
    And the "gateways" array contains 1 entries with matching name and url
    And the gateway named "Valid" is present

  @LAB-009
  Scenario: All invalid gateway URLs results in disabled
    Given the lab configuration contains gateways:
      | Name  | Url                       |
      | Bad1  | ftp://bad.example.com     |
      | Bad2  |                           |
    When the user calls GET /api/lab
    Then the response body has "enabled" equal to false
    And the "gateways" array contains 0 entries with matching name and url

  # --- API: same for all users ---

  @LAB-010
  Scenario: All users receive the same gateway list regardless of role
    Given the lab configuration contains gateways:
      | Name     | Url                            |
      | Shared   | https://gw.example.com/connect |
    When a user with role "participant" calls GET /api/lab
    And a user with role "coach" calls GET /api/lab
    Then both responses return the same gateways list

  # --- API: configuration source ---

  @LAB-002
  Scenario: Gateways are read from appsettings LabEnvironment.Gateways
    Given the appsettings.json contains a "LabEnvironment.Gateways" section with 2 entries
    When the user calls GET /api/lab
    Then the "gateways" array contains 2 entries with matching name and url

  # --- Frontend: navigation visibility ---

  @LAB-005
  Scenario: Lab navigation is hidden when lab is disabled
    Given the API returns lab enabled false
    When the user views the navigation
    Then the Lab navigation item is not visible

  @LAB-005
  Scenario: Lab navigation is visible when lab is enabled
    Given the API returns lab enabled true with 1 gateway
    When the user views the navigation
    Then the Lab navigation item is visible

  # --- Frontend: rendering links ---

  @LAB-006
  Scenario: Gateway links open in a new tab
    Given the API returns lab enabled true with gateways:
      | Name               | Url                              |
      | Gateway 1 (East US) | https://gw1.example.com/connect |
      | Gateway 2 (West US) | https://gw2.example.com/connect |
    When the user views the Lab page
    Then each gateway is rendered as a clickable link
    And each link opens in a new tab
    And each link text shows the gateway name

  @LAB-008
  Scenario: Gateway with empty name shows URL as label
    Given the API returns lab enabled true with gateways:
      | Name | Url                            |
      |      | https://gw.example.com/connect |
    When the user views the Lab page
    Then the link text shows "https://gw.example.com/connect"
