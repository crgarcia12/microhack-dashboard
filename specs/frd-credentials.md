# FRD: Credentials Display

**Feature ID:** CRED  
**PRD Section:** 4 — Credentials Display  
**Status:** Draft  
**Dependencies:** frd-auth (session-based team scoping)

---

## 1. Overview

The Credentials Display feature provides a read-only page where participants and coaches can view their team's provisioned credentials (cloud portal logins, VM passwords, API keys, etc.). Credentials are organized by category for quick scanning. The data is loaded from JSON files on disk and scoped per-team — users only see credentials belonging to their own team.

Credentials are managed externally (provisioned by event organizers via scripts). The portal never creates, edits, or deletes credentials.

---

## 2. User Stories

### US-CRED-1: View Team Credentials

**As a** participant or coach,  
**I want to** see my team's credentials grouped by category,  
**so that** I can quickly find the login details I need for the current challenge.

**Acceptance Criteria:**
- Credentials page is accessible from the main navigation for participants and coaches
- Only credentials belonging to the authenticated user's team are displayed
- Credentials are visually grouped by category (e.g., "Azure", "VM Access")
- Each credential entry displays a label (name) and its value
- Credential values are displayed as plain text (copyable)

### US-CRED-2: No Credentials Provisioned

**As a** participant or coach on a team with no credentials yet,  
**I want to** see a clear empty state message,  
**so that** I know credentials haven't been provisioned rather than thinking the page is broken.

**Acceptance Criteria:**
- When the team has no credentials, the page displays: "No credentials have been provisioned for your team yet."
- No empty category groups or blank tables are shown

### US-CRED-3: Organizer Excluded

**As an** event organizer,  
**I should not** see a Credentials navigation item,  
**because** organizers are not members of any team and credentials are team-scoped.

**Acceptance Criteria:**
- The Credentials link is hidden from the navigation for users with the organizer role
- If an organizer navigates directly to the Credentials URL, the API returns 403

---

## 3. Functional Requirements

### Data Source

| ID | Requirement |
|----|-------------|
| CRED-001 | Credentials are stored in a JSON file on disk at `hackcontent/credentials.json` |
| CRED-002 | The JSON file contains credentials for all teams; the API filters by the authenticated user's team |
| CRED-003 | The file is read at startup and cached in memory. Changes require an application restart |
| CRED-004 | If the file does not exist or is empty, all teams see the empty-state message |

### API

| ID | Requirement |
|----|-------------|
| CRED-010 | The API exposes `GET /api/credentials` returning the authenticated team's credentials |
| CRED-011 | Team identity is derived from the session — the endpoint accepts no team parameter from the client |
| CRED-012 | Unauthenticated requests receive `401 Unauthorized` |
| CRED-013 | Organizer-role requests receive `403 Forbidden` |
| CRED-014 | The response body is a JSON object containing `teamName` and a `categories` array |
| CRED-015 | If the team has no entry in the credentials file, return `200 OK` with an empty `categories` array |

### Frontend

| ID | Requirement |
|----|-------------|
| CRED-020 | A "Credentials" item appears in the navigation for participant and coach roles |
| CRED-021 | The Credentials page lives at route `/credentials` |
| CRED-022 | On load, the page calls `GET /api/credentials` and displays the result |
| CRED-023 | Credentials are rendered grouped by category using MUI `Card` components — one card per category |
| CRED-024 | Each category card shows the category name as a heading and a list of credential entries (label + value) |
| CRED-025 | When `categories` is empty, display: "No credentials have been provisioned for your team yet." |
| CRED-026 | While loading, display a skeleton or spinner |
| CRED-027 | Credential values must be selectable for copy. No masking or show/hide toggle is required (PoC scope) |

---

## 4. API Endpoint

### `GET /api/credentials`

**Auth:** Required (session cookie). Participant or Coach role only.

**Request:** No query parameters or body.

**Response `200 OK`:**

```json
{
  "teamName": "Team-01",
  "categories": [
    {
      "name": "Azure",
      "credentials": [
        { "label": "Portal Username", "value": "team01@contoso.com" },
        { "label": "Portal Password", "value": "P@ssw0rd123!" }
      ]
    },
    {
      "name": "VM Access",
      "credentials": [
        { "label": "SSH Host", "value": "10.0.1.4" },
        { "label": "SSH Username", "value": "hacker" },
        { "label": "SSH Password", "value": "Cha11enge!" }
      ]
    }
  ]
}
```

**Response `200 OK` (no credentials):**

```json
{
  "teamName": "Team-01",
  "categories": []
}
```

**Response `401 Unauthorized`:** No valid session.

**Response `403 Forbidden`:** Authenticated user is an organizer (not team-scoped).

---

## 5. Data Model

### `hackcontent/credentials.json` Schema

```json
{
  "teams": [
    {
      "teamName": "Team-01",
      "categories": [
        {
          "name": "Azure",
          "credentials": [
            { "label": "Portal Username", "value": "team01@contoso.com" },
            { "label": "Portal Password", "value": "P@ssw0rd123!" }
          ]
        }
      ]
    },
    {
      "teamName": "Team-02",
      "categories": []
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `teams` | array | Yes | Top-level array of per-team credential sets |
| `teams[].teamName` | string | Yes | Team identifier, must match team names in user config |
| `teams[].categories` | array | Yes | Grouped credential categories (may be empty) |
| `teams[].categories[].name` | string | Yes | Display name for the category |
| `teams[].categories[].credentials` | array | Yes | List of credential entries (may be empty) |
| `teams[].categories[].credentials[].label` | string | Yes | Human-readable label for the credential |
| `teams[].categories[].credentials[].value` | string | Yes | The credential value (plain text) |

---

## 6. Edge Cases

| Scenario | Expected Behavior |
|----------|-------------------|
| `credentials.json` file does not exist | API returns `200` with empty `categories` for all teams |
| `credentials.json` is malformed JSON | API logs a warning at startup; returns `200` with empty `categories` |
| Team exists in user config but not in `credentials.json` | API returns `200` with empty `categories` |
| A category has an empty `credentials` array | Category is omitted from the API response (do not show empty groups) |
| Credential value is very long (e.g., a connection string > 500 chars) | Value renders in full; CSS allows horizontal scroll or wrapping within the card |
| Credential value contains special characters (`<`, `>`, `&`, quotes) | Value is rendered as text, not HTML — no XSS risk |
| Multiple users on the same team load Credentials simultaneously | No issue — read-only data, no concurrency concern |

---

## 7. Error Handling

| Error Condition | HTTP Status | User-Facing Message |
|----------------|-------------|---------------------|
| No session cookie / invalid session | 401 | Redirect to login page |
| User is organizer role | 403 | "Credentials are not available for organizer accounts." |
| Server error reading credentials file | 500 | "Unable to load credentials. Please try again later." |

---

## 8. Dependencies

| Dependency | Reason |
|------------|--------|
| **frd-auth** | Session-based authentication provides the team identity used to scope credential access |

---

## 9. Out of Scope

- Credential editing, creation, or deletion via the portal
- Credential masking or show/hide toggle (PoC scope — all values shown as plain text)
- Real-time credential updates (requires restart to pick up changes)
- Credential rotation or expiry tracking
- Organizer view of all teams' credentials (can be added later)
