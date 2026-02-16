# FRD: Solution Viewing

**Feature:** Solution Viewing (Coach Only)
**PRD Section:** §3 — Solution Viewing (Coach Only)
**Status:** Draft
**Dependencies:** frd-auth (role-based access), frd-challenges (shared approval controls, Markdown rendering, content loading)

---

## 1. Overview

The Solution Viewing feature provides coaches with access to expected answers for each challenge. Solutions are loaded from Markdown files on disk at startup and served through a read-only API. The Solutions page mirrors the Challenges page in rendering and navigation but is restricted exclusively to the coach role. Participants must never see, access, or be aware of the Solutions section.

Coaches can browse any solution at any time regardless of the team's current challenge step, allowing them to prepare ahead and cross-reference answers while reviewing team work. The same approval controls (approve / revert / reset) available on the Challenges page are also present on the Solutions page so coaches can manage progression without switching views.

---

## 2. User Stories

### US-SOL-1: Browse All Solutions

**As a** coach,
**I want to** browse all available solutions regardless of my team's current challenge step,
**so that** I can prepare for upcoming challenges and review any answer at any time.

**Acceptance Criteria:**
- All solutions are listed in sequential order (solution-001, solution-002, …)
- Selecting a solution displays its full Markdown content rendered to HTML
- Navigation between solutions does not depend on the team's current step
- The solution list shows the total count of available solutions

### US-SOL-2: View Solution Content with Rich Formatting

**As a** coach,
**I want to** see solution content rendered with the same rich formatting as challenges,
**so that** code blocks, images, tables, and headings display correctly.

**Acceptance Criteria:**
- Headings (H1–H6) render with correct hierarchy
- Fenced code blocks render with syntax highlighting
- Inline images referencing `media/` resolve and display correctly
- Ordered and unordered lists render correctly
- Tables (including Skillable-style pipe tables) render correctly
- The rendering is visually identical to challenge content rendering

### US-SOL-3: Approve/Revert/Reset from Solutions Page

**As a** coach,
**I want to** approve, revert, or reset my team's challenge progression directly from the Solutions page,
**so that** I don't have to switch to the Challenges page to manage progression.

**Acceptance Criteria:**
- Approve, Revert, and Reset buttons are present on the Solutions page
- Approve advances the team to the next challenge (keyboard shortcut: `Ctrl+Enter`)
- Revert sends the team back to the previous challenge
- Reset returns the team to challenge 1 and clears all progress
- Button behavior is identical to the Challenges page controls
- Real-time updates via SignalR reflect progression changes on both pages

### US-SOL-4: Solutions Hidden from Participants

**As a** participant,
**I want** the Solutions section to be completely hidden,
**so that** I cannot accidentally or intentionally view answer keys.

**Acceptance Criteria:**
- The Solutions navigation item does not appear for participants
- Direct navigation to the Solutions URL redirects participants away (to Challenges)
- API requests to solution endpoints from participant sessions return `403 Forbidden`
- No solution data is included in any participant-facing API response

---

## 3. Functional Requirements

### Content Loading

| ID | Requirement |
|----|-------------|
| SOL-001 | The API MUST load all solution files from `hackcontent/solutions/` at startup, matching the pattern `solution-###.md` where `###` is a zero-padded three-digit number |
| SOL-002 | Solutions MUST be sorted by their numeric suffix in ascending order (001, 002, 003, …) |
| SOL-003 | Each solution MUST be associated with its corresponding challenge by matching number (solution-001 → challenge-001) |
| SOL-004 | If no solution files are found at startup, the solutions list endpoint MUST return an empty array and the Solutions page MUST display "No solutions loaded — add Markdown files to hackcontent/solutions/" |
| SOL-005 | Solution files MUST be loaded once at startup; adding new files requires an application restart |

### Content Rendering

| ID | Requirement |
|----|-------------|
| SOL-006 | The API MUST return solution content as raw Markdown; the frontend MUST render it to HTML |
| SOL-007 | Markdown rendering MUST support: headings (H1–H6), fenced code blocks with language-tagged syntax highlighting, inline images, ordered/unordered lists, and pipe-delimited tables |
| SOL-008 | Image references in solution Markdown (e.g., `![alt](media/0010.png)`) MUST resolve to the media serving endpoint `/api/solutions/media/{filename}` |
| SOL-009 | The frontend MUST use the same Markdown rendering component and configuration as the Challenges page |

### Access Control

| ID | Requirement |
|----|-------------|
| SOL-010 | All solution API endpoints MUST require an authenticated session with the `coach` role |
| SOL-011 | Requests from `participant` sessions to any solution endpoint MUST receive a `403 Forbidden` response with body `{ "error": "Forbidden" }` |
| SOL-012 | Requests from `organizer` sessions to solution endpoints MUST receive a `403 Forbidden` response — solutions are coach-only, not organizer-visible |
| SOL-013 | The frontend MUST NOT render the Solutions navigation link for any role other than `coach` |
| SOL-014 | If a non-coach user navigates directly to `/solutions` (e.g., by typing the URL), the frontend MUST redirect them to `/challenges` |

### Approval Controls

| ID | Requirement |
|----|-------------|
| SOL-015 | The Solutions page MUST include Approve, Revert, and Reset controls with identical behavior to the Challenges page |
| SOL-016 | The Approve button MUST advance the team's current step by 1, the Revert button MUST decrement it by 1 (minimum: step 1), and the Reset button MUST set it to step 1 and clear all recorded times |
| SOL-017 | The Approve action MUST be triggerable via the keyboard shortcut `Ctrl+Enter` when the Solutions page is focused |
| SOL-018 | Approval control state changes MUST be broadcast via SignalR to all connected team members |

### Navigation

| ID | Requirement |
|----|-------------|
| SOL-019 | The Solutions page MUST display a sidebar or list showing all solutions with their sequence numbers and titles |
| SOL-020 | The currently viewed solution MUST be visually highlighted in the navigation list |
| SOL-021 | The team's current challenge step MUST be indicated in the solution list (e.g., a marker or badge) so the coach knows where the team is |

---

## 4. API Endpoints

All endpoints are prefixed with `/api` and require cookie-based session authentication.

### GET /api/solutions

**Description:** Returns the list of all available solutions with metadata.

**Authorization:** Coach only (role = `coach`).

**Response `200 OK`:**
```json
{
  "solutions": [
    {
      "number": 1,
      "title": "Azure Migration & Modernization MicroHack",
      "fileName": "solution-001.md"
    },
    {
      "number": 2,
      "title": "Discovery and Assessment",
      "fileName": "solution-002.md"
    }
  ],
  "totalCount": 6,
  "currentStep": 3
}
```

- `number`: The solution's sequence number parsed from the filename
- `title`: Extracted from the first H1 heading (`# ...`) in the Markdown file; falls back to the filename if no H1 is found
- `fileName`: The source filename
- `totalCount`: Total number of loaded solutions
- `currentStep`: The team's current challenge step (from the coach's team context)

**Response `403 Forbidden`:**
```json
{ "error": "Forbidden" }
```
Returned when the authenticated user's role is not `coach`.

**Response `401 Unauthorized`:**
```json
{ "error": "Unauthorized" }
```
Returned when no valid session cookie is present.

---

### GET /api/solutions/{number}

**Description:** Returns the full Markdown content of a single solution.

**Path Parameters:**
- `number` (integer, required): The solution sequence number (e.g., `1`, `2`, `3`)

**Authorization:** Coach only (role = `coach`).

**Response `200 OK`:**
```json
{
  "number": 1,
  "title": "Azure Migration & Modernization MicroHack",
  "fileName": "solution-001.md",
  "content": "# Azure Migration & Modernization MicroHack\n\nThis MicroHack scenario walks through..."
}
```

- `content`: The raw Markdown string of the solution file. The frontend renders this to HTML.

**Response `404 Not Found`:**
```json
{ "error": "Solution not found", "number": 99 }
```
Returned when no solution with the given number exists.

**Response `403 Forbidden`:**
```json
{ "error": "Forbidden" }
```

**Response `401 Unauthorized`:**
```json
{ "error": "Unauthorized" }
```

---

### GET /api/solutions/media/{filename}

**Description:** Serves a static media file (image) referenced by solution Markdown content.

**Path Parameters:**
- `filename` (string, required): The media file name (e.g., `0010.png`, `clone-url.png`)

**Authorization:** Coach only (role = `coach`).

**Response `200 OK`:**
- Content-Type: `image/png` (or appropriate MIME type based on file extension)
- Body: Raw binary file content
- Cache-Control: `public, max-age=86400` (media files are static; cache for 24 hours)

**Response `404 Not Found`:**
```json
{ "error": "Media file not found", "filename": "nonexistent.png" }
```
Returned when the requested file does not exist in `hackcontent/solutions/media/`.

**Response `403 Forbidden`:**
```json
{ "error": "Forbidden" }
```

**Response `401 Unauthorized`:**
```json
{ "error": "Unauthorized" }
```

**Security:** The endpoint MUST validate that `filename` contains no path traversal characters (`..`, `/`, `\`). Requests with path traversal attempts MUST return `400 Bad Request`.

---

## 5. Data Model

### Solution File Structure on Disk

```
hackcontent/
└── solutions/
    ├── solution-001.md
    ├── solution-002.md
    ├── solution-003.md
    ├── solution-004.md
    ├── solution-005.md
    ├── solution-006.md
    └── media/
        ├── 0010.png
        ├── 0020.png
        ├── clone-url.png
        └── ...
```

### SolutionMetadata (API model)

| Field | Type | Description |
|-------|------|-------------|
| `number` | `int` | Sequence number parsed from filename (e.g., `1` from `solution-001.md`) |
| `title` | `string` | First H1 heading from the Markdown file, or filename if no H1 found |
| `fileName` | `string` | Original filename on disk |

### SolutionDetail (API model, extends SolutionMetadata)

| Field | Type | Description |
|-------|------|-------------|
| `number` | `int` | Sequence number |
| `title` | `string` | First H1 heading or filename |
| `fileName` | `string` | Original filename |
| `content` | `string` | Raw Markdown content of the solution file |

### In-Memory Storage

Solutions are loaded into an in-memory list at startup (same pattern as challenges). The service holds a `List<SolutionDetail>` sorted by `number`. No database or file-based state is needed — solution content is read-only.

---

## 6. Edge Cases

| Scenario | Expected Behavior |
|----------|-------------------|
| **Participant requests `GET /api/solutions`** | `403 Forbidden` — no solution data leaked |
| **Participant requests `GET /api/solutions/1`** | `403 Forbidden` |
| **Participant requests `GET /api/solutions/media/0010.png`** | `403 Forbidden` |
| **Organizer requests `GET /api/solutions`** | `403 Forbidden` — solutions are coach-only per PRD |
| **No solution files in `hackcontent/solutions/`** | `GET /api/solutions` returns `{ "solutions": [], "totalCount": 0, "currentStep": 1 }`. Frontend shows empty-state message |
| **Solution file exists but is empty (0 bytes)** | Loaded with `content: ""` and `title` falls back to filename |
| **Solution file has no H1 heading** | `title` falls back to the filename (e.g., `"solution-003.md"`) |
| **Requested solution number does not exist** | `GET /api/solutions/99` returns `404 Not Found` |
| **Requested solution number is not a positive integer** | `GET /api/solutions/abc` returns `400 Bad Request` |
| **Media file does not exist** | `GET /api/solutions/media/missing.png` returns `404 Not Found` |
| **Media filename contains path traversal (`../`)** | `GET /api/solutions/media/../../etc/passwd` returns `400 Bad Request` |
| **Media file has non-image extension** | Serve with the appropriate MIME type based on extension; only allow known extensions (`.png`, `.jpg`, `.jpeg`, `.gif`, `.svg`). Reject others with `400 Bad Request` |
| **Solution count differs from challenge count** | No error — solutions and challenges are independent sets. Some challenges may lack solutions and vice versa |
| **Very large Markdown file (>1 MB)** | Serve normally — no size cap for PoC |
| **Coach from team A requests solutions** | Solutions are global content, not team-scoped. All coaches see the same solutions. The `currentStep` in the response reflects the requesting coach's team |

---

## 7. Error Handling

| Error Condition | HTTP Status | Response Body | UI Behavior |
|-----------------|-------------|---------------|-------------|
| No session cookie | `401 Unauthorized` | `{ "error": "Unauthorized" }` | Redirect to login page |
| Non-coach role | `403 Forbidden` | `{ "error": "Forbidden" }` | Redirect to `/challenges` (frontend route guard) |
| Solution not found | `404 Not Found` | `{ "error": "Solution not found", "number": N }` | Show "Solution not found" message in content area |
| Invalid solution number | `400 Bad Request` | `{ "error": "Invalid solution number" }` | Should not occur via UI; defensive API handling |
| Media file not found | `404 Not Found` | `{ "error": "Media file not found", "filename": "X" }` | Broken image placeholder displayed inline |
| Path traversal in media filename | `400 Bad Request` | `{ "error": "Invalid filename" }` | Should not occur via UI; defensive API handling |
| Disallowed media file extension | `400 Bad Request` | `{ "error": "Unsupported file type" }` | Broken image placeholder displayed inline |
| Server error loading solutions | `500 Internal Server Error` | `{ "error": "Internal server error" }` | Show generic error banner on Solutions page |

---

## 8. Dependencies

| Dependency | Type | Detail |
|------------|------|--------|
| **frd-auth** | Hard | Role checking — the `coach` role must be verified on every solution endpoint. Session cookie authentication must be in place |
| **frd-challenges** | Shared | Markdown rendering component is shared with Challenges. Approval controls (approve/revert/reset) use the same API endpoints and SignalR events as Challenges. Content loading pattern (Markdown files from `hackcontent/` at startup) is identical |
| **SignalR hub** | Shared | Approval actions on the Solutions page trigger the same SignalR events used by the Challenges page — no separate hub needed |
| **hackcontent/solutions/** | Data | Solution Markdown files and media must be present on disk at startup |

---

## 9. Out of Scope

- Editing or uploading solutions through the UI
- Mapping solutions to challenges by any mechanism other than matching sequence numbers
- Full-text search across solutions
- Solution-specific annotations or coach notes
- Per-team customized solutions
- WCAG accessibility beyond MUI defaults (per PRD)
