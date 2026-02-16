# FRD: Lab Environment Access

**Feature ID:** lab-env
**Status:** Draft
**Depends on:** frd-auth (authenticated session required)
**PRD section:** §8 — Lab Environment Access (Optional)

---

## 1. Overview

Lab Environment Access provides authenticated users with connection URL(s) for a browser-based remote desktop gateway. The feature is **entirely optional** — it is only visible when at least one gateway endpoint is configured in the application settings. All authenticated users see the same endpoints; there is no per-team mapping.

---

## 2. User Stories

### US-LAB-1: View lab environment links

**As a** participant or coach,
**I want to** see connection URLs for the remote desktop gateway,
**So that** I can open my lab VM in the browser without installing any software.

**Acceptance criteria:**
- Given gateway endpoint(s) are configured, when I navigate to the Lab page, then I see all configured gateway URLs displayed as clickable links
- Given no gateway endpoints are configured, then the Lab page and its navigation link are completely hidden

### US-LAB-2: Hidden when unconfigured

**As an** event organizer,
**I want** the lab section to be invisible when no gateway is configured,
**So that** the UI stays clean for events that don't use lab VMs.

**Acceptance criteria:**
- Given zero gateway endpoints in configuration, when any user logs in, then the Lab navigation item does not appear
- Given at least one gateway endpoint, when any user logs in, then the Lab navigation item appears

---

## 3. Functional Requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| LAB-001 | The API exposes `GET /api/lab` returning the lab environment configuration | Must |
| LAB-002 | Gateway endpoints are configured via `appsettings.json` under `LabEnvironment.Gateways` | Must |
| LAB-003 | If no gateways are configured (missing key, empty array, or null), `GET /api/lab` returns `{"enabled": false, "gateways": []}` | Must |
| LAB-004 | If one or more gateways are configured, `GET /api/lab` returns `{"enabled": true, "gateways": [...]}` | Must |
| LAB-005 | The frontend hides the Lab navigation item and page when `enabled` is `false` | Must |
| LAB-006 | The frontend renders each gateway as a clickable link that opens in a new tab | Must |
| LAB-007 | `GET /api/lab` requires an authenticated session (returns 401 if not logged in) | Must |
| LAB-008 | Each gateway entry includes a `name` (display label) and `url` (connection endpoint) | Must |
| LAB-009 | Gateway URLs must be valid absolute URLs (`https://` or `http://`); invalid entries are silently excluded from the response | Should |
| LAB-010 | The API returns the same gateway list for all users regardless of team or role | Must |

---

## 4. API Endpoints

### `GET /api/lab`

Returns the lab environment configuration for the current session.

**Auth:** Required (session cookie). Returns `401 Unauthorized` if no valid session.

**Response `200 OK` — gateways configured:**

```json
{
  "enabled": true,
  "gateways": [
    { "name": "Gateway 1 (East US)", "url": "https://gw1.example.com/connect" },
    { "name": "Gateway 2 (West US)", "url": "https://gw2.example.com/connect" }
  ]
}
```

**Response `200 OK` — no gateways configured:**

```json
{
  "enabled": false,
  "gateways": []
}
```

> The endpoint always returns 200. The `enabled` flag drives frontend visibility — there is no 404 case.

---

## 5. Data Model

### Configuration schema (`appsettings.json`)

```json
{
  "LabEnvironment": {
    "Gateways": [
      { "Name": "Gateway 1 (East US)", "Url": "https://gw1.example.com/connect" },
      { "Name": "Gateway 2 (West US)", "Url": "https://gw2.example.com/connect" }
    ]
  }
}
```

### Strongly-typed settings class

```csharp
public class LabEnvironmentOptions
{
    public const string SectionName = "LabEnvironment";
    public List<GatewayEntry> Gateways { get; set; } = [];
}

public class GatewayEntry
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
```

### API response DTO

```csharp
public record LabEnvironmentResponse(bool Enabled, List<GatewayDto> Gateways);
public record GatewayDto(string Name, string Url);
```

---

## 6. Edge Cases

| Scenario | Behavior |
|----------|----------|
| `LabEnvironment` key missing from config | `enabled: false`, empty gateways array |
| `Gateways` is `null` | `enabled: false`, empty gateways array |
| `Gateways` is empty array `[]` | `enabled: false`, empty gateways array |
| Single gateway configured | `enabled: true`, array with one entry |
| Multiple gateways configured | `enabled: true`, array with all valid entries |
| Gateway entry has empty `Url` | Entry excluded from response (LAB-009) |
| Gateway entry has invalid URL (not `http://` or `https://`) | Entry excluded from response (LAB-009) |
| Gateway entry has empty `Name` | Entry included; frontend displays the URL as the label |
| All gateway entries are invalid | `enabled: false`, empty gateways array (no valid entries remain) |
| User is not authenticated | `401 Unauthorized` |

---

## 7. Error Handling

| Error | HTTP Status | Response |
|-------|-------------|----------|
| Not authenticated | `401` | Standard unauthorized response |
| Server error reading config | `500` | `{ "error": "Failed to load lab configuration" }` |

No user-facing error states exist beyond 401. The endpoint always returns a valid JSON shape on success — the `enabled` flag handles the "not configured" case gracefully.

---

## 8. Dependencies

| Dependency | Reason |
|------------|--------|
| **frd-auth** | `GET /api/lab` requires a valid authenticated session |
| **appsettings.json** | Gateway endpoints read from application configuration |

No external service dependencies. The endpoint reads from local configuration only.

---

## 9. Out of Scope

- Per-team VM mapping (PRD §8 known limitation)
- VM provisioning or lifecycle management
- Gateway health checks or availability monitoring
- Admin UI for managing gateway configuration (config file only)
