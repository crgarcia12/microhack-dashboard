# HackboxConsole — Product Requirements Document

## Product Overview

**HackboxConsole** is an event management portal for running hackathons and technical challenges. It provides a guided, step-by-step experience where participant teams work through a series of challenges under the supervision of a coach, while event organizers maintain oversight across all teams from a central dashboard.

The product serves three distinct user personas across a single shared platform, with each team's data kept private and separate.

---

## Target Users

| Persona | Description |
|---------|-------------|
| **Participant (Hacker)** | A hackathon team member who works through challenges, views lab credentials, and tracks time |
| **Coach** | A facilitator assigned to one team who reviews solutions, approves challenge progression, and controls the team's pace |
| **Event Organizer (Tech Lead)** | An administrator who monitors all teams, manages event-wide timing, and performs bulk operations |

---

## Product Goals

1. Deliver a structured, guided hackathon experience with sequenced challenges
2. Give coaches full control over their team's progression pace
3. Provide event organizers a single-pane-of-glass view across all teams
4. Keep each team's progress, timing, and credentials completely isolated
5. Minimize setup effort — pre-configured users, challenges loaded from files, credentials provisioned externally
6. Present a modern, visually appealing interface with a polished feel

---

## Design Direction

The UI follows a modern, visually rich aesthetic using a **purple and blue** color palette with gradients, glows, and subtle animations. The frontend is built with **React** (via Next.js App Router) using **Material UI (MUI)** as the component library for consistent, production-quality components out of the box.

**Key design principles:**
- Dark or semi-dark theme with purple/blue accent colors and gradient highlights
- MUI components for all interactive elements (buttons, cards, tables, navigation, dialogs)
- Smooth transitions and micro-animations for state changes (challenge progression, timer updates)
- Clean typography and generous spacing
- Responsive layout (desktop-first, but usable on tablets)

**Accessibility:** Formal WCAG compliance is out of scope for the PoC. MUI provides built-in keyboard navigation and ARIA support by default, which is sufficient.

---

## Features

### 1. User Login & Role-Based Experience

Users log in with a username and password. Once logged in, the interface adapts to their role — participants see challenges, credentials, and a timer; coaches see everything participants see plus answer keys and approval controls; event organizers see a management dashboard.

**Key behaviors:**
- Login is case-insensitive for usernames
- Authentication is performed by matching username and password against the users configuration file (passwords stored in plain text — PoC only)
- On successful login, the server creates a session ID stored in a cookie
- Users stay logged in across page refreshes until they explicitly log out
- Sessions do not expire — users remain logged in until explicit logout or server restart
- Navigation menus show only the sections relevant to the user's role
- Username and role are displayed in the navigation bar

**Error handling:**
- Invalid credentials display "Invalid username or password"
- Empty username or password fields show "Please fill in all fields"
- No account lockout — users can retry immediately

---

### 2. Challenge Progression

The core experience. Challenges are presented one at a time in a fixed sequence. Participants can view the current challenge and any they have already completed, but cannot skip ahead.

**For Participants:**
- View the current challenge with rich text formatting (headings, code blocks, images, lists)

**Content format:**
- Challenges are authored in Markdown and stored in `hackcontent/challenges/challenge-###.md`
- Solutions are stored in `hackcontent/solutions/solution-###.md`
- Solution media (screenshots, diagrams) is stored in `hackcontent/solutions/media/` as PNG files
- Markdown is rendered to HTML in the frontend with support for headings, code blocks with syntax highlighting, inline images, lists, and tables (Skillable-style format)
- Files are numbered sequentially (001, 002, ...) and loaded in order at startup
- If no challenge files are found at startup, the Challenges page displays "No challenges loaded — add Markdown files to hackcontent/challenges/"
- Browse previously completed challenges
- See locked challenges they haven't reached yet
- A progress bar shows how far they've progressed through the challenge set
- When all challenges are completed, a celebration screen appears
**Real-time updates:**
- Changes made by the coach appear automatically within a few seconds — no manual refresh needed
- The backend uses **SignalR** to push state changes to connected clients in real time
- When a coach approves, reverts, or resets, all team members see the update instantly
- If the SignalR connection drops, the client falls back to polling every 5 seconds until reconnected

**For Coaches:**
- **Approve**: Advance the team to the next challenge (keyboard shortcut: `Ctrl+Enter`)
- **Revert**: Send the team back to the previous challenge if they need to rework it
- **Reset**: Return the team to challenge 1 and clear all progress
- These controls are available from both the Challenges and Solutions pages

**Error handling:**
- If a coach action fails (e.g., reverting past challenge 1, or approving past the last challenge), the UI shows a brief error notification
- No concurrent-coach protection — the PoC assumes one coach per team

---

### 3. Solution Viewing (Coach Only)

Coaches have access to a separate Solutions section containing the expected answers for each challenge. Solutions are always fully accessible regardless of the team's current step, allowing coaches to prepare and review ahead of time.

**Key behaviors:**
- All solutions are browsable at any time
- Same approval controls (approve / revert / reset) are available on the Solutions page
- Participants never see the Solutions section — it is hidden entirely from their view

---

### 4. Credentials Display

Each team has a set of credentials (cloud portal logins, VM passwords, etc.) that are displayed in a dedicated Credentials page. Credentials are organized by category for easy scanning.

**Key behaviors:**
- Credentials are specific to each team — a team only sees their own
- Credentials are grouped visually by category (e.g., "Azure", "VM Access")
- The page handles the case where no credentials have been provisioned yet
- Credentials are read-only in the portal — they are managed by event organizers outside the application

---

### 5. Timer & Challenge Timing

The application automatically tracks how long each team spends on each challenge. The timer runs in the background and is managed automatically as the coach advances through challenges.

**Automatic timing:**
- A timer starts when the team begins their first challenge
- When the coach approves a challenge, the elapsed time for that challenge is recorded
- The timer resets and restarts for the next challenge
- When all challenges are completed, the timer stops
- If the coach reverts a challenge, the recorded time for that challenge is removed
- If the coach resets to the beginning, all recorded times are cleared

**Manual timer page:**
- A visible timer page is available for participants and coaches
- Displays elapsed time in hours, minutes, and seconds
- Includes start, stop, and reset controls

**Known limitation:** The visible timer on the Timer page and the automatic background timer run independently — this is by design. The Timer page is a local-only stopwatch for the team's own use, while the system tracks actual challenge durations separately for reporting.

---

### 6. Event Organizer Dashboard (Tech Lead)

A management dashboard that gives event organizers visibility into every team's status and the ability to control the event at scale.

**Monitoring:**
- See every team's current challenge step at a glance
- View each team's timer status (running, stopped, elapsed time)
- See the total number of challenges available

**Bulk operations:**
- Advance, revert, or reset any individual team's challenge
- Apply the same operations across all teams at once
- Start, stop, or reset stopwatches for individual teams or all teams simultaneously

**Known limitation:** The dashboard does not currently support real-time auto-refresh — organizers must manually reload to see updated team statuses.

---

### 7. Team Isolation (Multi-Tenancy)

Multiple teams share one application instance, but each team's data is completely private. A team cannot see another team's challenge progress, credentials, or timer status.

**Key behaviors:**
- Each user belongs to exactly one team
- Multiple users can belong to the same team (typically one participant account + one coach account)
- A user's team assignment is fixed at login — it cannot be changed during a session
- Event organizers operate across all teams but are not themselves members of any team
- Teams are defined in the user configuration — adding or removing teams requires an application restart
- **API enforcement:** Every API request is scoped to the authenticated user's team, derived from their session. No endpoint accepts a team ID from the client. Event organizer endpoints require the organizer role and can access all teams

---

### 8. Lab Environment Access (Optional)

For hackathons that require cloud or VM-based lab environments, the platform can integrate with a browser-based remote desktop gateway. This allows participants to connect to their lab VM directly from the browser without installing any software.

**Key behaviors:**
- The application provides the connection endpoint URL(s) for the remote desktop gateway
- Multiple gateway endpoints can be configured for redundancy
- Lab VMs are provisioned separately as part of event setup
- This feature is entirely optional — if no gateway is configured, it is not shown

**Known limitation:** There is currently no automatic mapping between teams and their specific lab VMs — the same gateway endpoints are shown to all users.

---

## User Workflows

### Participant Workflow

1. Log in with team credentials
2. View the current challenge on the Challenges page
3. Work on the challenge (using lab environment if applicable)
4. Check the Credentials page for any access details needed
5. Wait for coach to approve and advance to the next challenge
6. Repeat until all challenges are completed
7. Celebration screen appears on completion

### Coach Workflow

1. Log in with coach credentials
2. Review the current challenge on the Challenges page
3. Consult the corresponding solution on the Solutions page
4. When the team completes the challenge, click **Approve** to advance them
5. Use **Revert** if the team needs to redo work, or **Reset** to start over
6. Monitor the timer page or rely on automatic challenge timing
7. Repeat for each challenge

### Event Organizer Workflow

1. Log in with tech lead credentials
2. Open the dashboard to see all team statuses
3. Use bulk controls to synchronize timing or manage the event flow
4. Advance, revert, or reset individual teams as needed
5. After the event, review challenge completion times for all teams

---

## Constraints and Limitations

| Area | Constraint |
|------|-----------|
| **Data persistence** | All application state (users, teams, challenge progress, timer data) is stored in JSON files on disk. Challenge and solution content is loaded from Markdown files at startup. This is suitable for single-instance local development; a database would be needed for production |
| **User management** | Users are pre-configured before the event — there is no self-registration or user management UI |
| **Challenge content** | Challenges and solutions are Markdown files in `hackcontent/` loaded at startup — adding new ones requires a restart |
| **Credentials** | Managed externally via scripts, not editable within the portal |
| **Team management** | Teams are derived from user configuration — no team administration UI exists |
| **Real-time updates** | Participant screens update within ~5 seconds of a coach action (not instant) |
| **Timer sync** | The visible Timer page timer is independent from the system's automatic challenge timing |
| **Dashboard completeness** | The new dashboard UI has limited functionality; full controls are in the legacy interface |
| **Lab VM mapping** | No per-team VM assignment — gateway endpoints are shared across all teams |

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Time from event setup to first participant login | < 30 minutes |
| Participant onboarding (login to first challenge visible) | < 1 minute |
| Coach action latency (approve → participant sees next challenge) | < 5 seconds |
| Concurrent teams supported per instance | 20+ |
| Event organizer setup effort | Single script execution for infrastructure + credentials |
