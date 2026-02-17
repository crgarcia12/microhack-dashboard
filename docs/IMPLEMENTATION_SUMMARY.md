# Hack Lifecycle Management - Implementation Summary

## Overview

Successfully implemented a complete hack lifecycle management system as requested in the problem statement. The system allows tech leads to configure microhack events, manage teams and coaches, and launch the hack with real-time synchronization across all users.

## Requirements vs. Implementation

### ✅ Original Requirements

**From problem statement:**
> "At the beginning, there will be some sort of configuration screen where the tech lead can decide what microhack content (challenges + solutions) will be used. the lead will also set the teams coaches etc."

**Implementation:**
- Created `/hack-config` page with full configuration UI
- Content path selection
- Dynamic team creation with member assignment
- Coach management interface
- All changes persist to database or file storage

---

> "while all that is happening, coaches and hackers that login should have some waiting screen, that tells them that the hack did not start. use the splashcreen of the beggining as a template, you could have those falling stars in the back and say something like waiting, the hack will start soon or whatever."

**Implementation:**
- Created `WaitingScreen` component reusing splash screen design
- Falling stars/particles animation (identical visual style)
- Animated "Waiting for hack to start..." message with loading ellipsis
- Shows automatically to non-tech-lead users when hack is in "waiting" or "configuration" state

---

> "After the techlead is ok. he will lauch the hack, then all students get suddenly the content and the clock starts."

**Implementation:**
- "Launch Hack" button on config page with confirmation dialog
- SignalR broadcasts `hackLaunched` event to all connected clients
- Waiting screen disappears instantly for all users
- Hack state transitions from "waiting" → "active"
- Global clock starts immediately

---

> "in the dasboard of the lead, the clocks of the teams should be ticking, so the api could send the start datetime, and the UI will render the diff changes with the current time. so they dont have to refresh"

**Implementation:**
- API sends `startedAt` ISO timestamp in hack state
- Dashboard calculates elapsed time client-side: `currentTime - startedAt`
- Timer updates every second via `setInterval`
- No API polling required - completely client-side calculation
- Global hack timer displayed prominently at top of dashboard
- All team timers sync to same start time

## Architecture

### State Machine

```
┌─────────────┐
│ not_started │
└──────┬──────┘
       │
       │ Tech lead saves config
       ↓
┌─────────────┐
│   waiting   │ ← Participants see waiting screen
└──────┬──────┘
       │
       │ Tech lead clicks "Launch Hack"
       ↓
┌─────────────┐
│   active    │ ← Content visible, timers running
└─────────────┘
```

### Backend Stack

- **Language**: C# / .NET 10
- **Framework**: Minimal API
- **Storage**: Dual implementation
  - EF Core: SQL Server & SQLite
  - File-based: JSON files in `config-data/`
- **Real-time**: SignalR Hub
- **Models**: HackState, HackConfig
- **Endpoints**: 4 new REST endpoints

### Frontend Stack

- **Framework**: Next.js 16 (App Router)
- **Language**: TypeScript
- **UI**: Material-UI (MUI)
- **State**: React Context + Hooks
- **Real-time**: SignalR client (`@microsoft/signalr`)
- **Components**: WaitingScreen, HackStateProvider, Config page

## Files Created/Modified

### Backend (15 files)

**New Models:**
- `src/api/Models/HackState.cs` - Lifecycle state model
- `src/api/Models/HackConfig.cs` - Configuration model with teams/coaches

**New Entities:**
- `src/api/data/Entities/HackStateEntity.cs` - Database entity
- `src/api/data/Entities/HackConfigEntity.cs` - Configuration entity (JSON serialization)

**New Repositories:**
- `src/api/data/IHackStateRepository.cs`
- `src/api/data/EfCore/EfHackStateRepository.cs`
- `src/api/data/File/FileHackStateRepository.cs`
- `src/api/data/IHackConfigRepository.cs`
- `src/api/data/EfCore/EfHackConfigRepository.cs`
- `src/api/data/File/FileHackConfigRepository.cs`

**New Service:**
- `src/api/Services/IHackStateService.cs`
- `src/api/Services/HackStateService.cs`

**New Endpoints:**
- `src/api/Endpoints/HackStateEndpoints.cs`

**Modified:**
- `src/api/data/HackboxDbContext.cs` - Added HackState and HackConfig DbSets
- `src/api/Program.cs` - Registered new services and endpoints

### Frontend (5 files)

**New Context:**
- `src/web/src/contexts/HackStateContext.tsx` - Global state management with SignalR

**New Component:**
- `src/web/src/app/components/WaitingScreen.tsx` - Full-screen animated waiting overlay

**New Page:**
- `src/web/src/app/(authenticated)/hack-config/page.tsx` - Configuration UI

**Modified:**
- `src/web/src/app/(authenticated)/layout.tsx` - Integrated HackStateProvider and waiting screen logic
- `src/web/src/app/(authenticated)/dashboard/page.tsx` - Real-time timer calculation

### Documentation (2 files)

- `docs/hack-lifecycle.md` - Complete feature documentation
- `docs/IMPLEMENTATION_SUMMARY.md` - This file

## Key Features

### 1. Configuration Screen (`/hack-config`)

**Features:**
- Content path selection (default: "hackcontent")
- Team management:
  - Add/remove teams dynamically
  - Assign members to each team
  - Visual chip-based member display
- Coach management:
  - Add/remove coaches
  - Chip-based display
- Status indicator showing current hack state
- "Save Configuration" button (transitions to "waiting")
- "Launch Hack" button (confirmation dialog, transitions to "active")

**Access Control:**
- Only visible to tech leads
- Only accessible in navigation when hack is not active
- Returns 403 for non-tech-leads

### 2. Waiting Screen

**Visual Design:**
- Reuses splash screen aesthetics:
  - Floating particles (falling stars) with staggered animations
  - Gradient background with radial purple glow
  - Scan line animation effect
  - Bottom gradient bar
- Main message: "Waiting for hack to start"
- Subtitle: "The tech lead is configuring the event..."
- Animated ellipsis (... → .. → .)
- Footer: "You'll be notified when it begins"

**Behavior:**
- Full-screen overlay (z-index: 9998)
- Shown automatically to non-tech-lead users
- Displayed when hack state is "waiting" or "configuration"
- Disappears instantly via SignalR when hack launches
- No manual refresh needed

### 3. Real-time Timers

**Implementation:**
- **Server sends**: `startedAt` timestamp (ISO 8601 string)
- **Client calculates**: `elapsed = currentTime - startedAt`
- **Update frequency**: Every 1 second via `setInterval`
- **Format**: `HH:MM:SS` (e.g., "01:23:45")

**Dashboard Display:**
- **Global timer** (top of page):
  - Shows hack start time
  - Shows global elapsed time (large, gradient text)
  - Timer icon indicator
- **Team timers** (table):
  - Each team shows same elapsed time
  - Synced to global hack start
  - No per-team variation

**Performance:**
- Zero API calls for timer updates
- Efficient client-side calculation
- Minimal re-renders (only timer display updates)

### 4. SignalR Real-time Events

**Events emitted by backend:**
- `hackStateChanged` - When state transitions
- `hackLaunched` - When hack is launched (includes full state)

**Frontend subscriptions:**
- HackStateContext listens for both events
- Automatically updates React state
- Triggers component re-renders
- Waiting screen disappears instantly

## Testing Completed

### Build Verification ✅

```bash
# Backend build
cd src/api && dotnet build
# Result: Build succeeded, 0 errors

# Frontend build
cd src/web && npm run build
# Result: Build succeeded, all pages generated
```

### Code Quality ✅

- TypeScript strict mode: No type errors
- All imports resolved correctly
- Navigation routes: 15 total routes generated
- Static pages: 12 static, 3 dynamic

## Manual Testing Guide

### Prerequisites

1. Start the backend API:
   ```bash
   cd src/api
   dotnet run
   ```

2. Start the frontend:
   ```bash
   cd src/web
   npm run dev
   ```

### Test Scenario 1: Configuration Flow

1. **Login as Tech Lead**:
   - Navigate to http://localhost:3000/login
   - Username: `techlead`, Password: (configured in system)

2. **Access Configuration**:
   - Click "Config" in top navigation
   - Verify current status shows "not_started" or "waiting"

3. **Add Teams**:
   - Enter team name: "Team Alpha"
   - Click "Add Team"
   - Click inside "Add member..." field
   - Type "Alice" and press Enter
   - Type "Bob" and press Enter
   - Verify chips appear with "Alice" and "Bob"

4. **Add Coaches**:
   - Enter coach name: "Coach Smith"
   - Click "Add Coach"
   - Verify chip appears

5. **Save Configuration**:
   - Click "Save Configuration"
   - Verify success toast appears
   - Verify status changes to "waiting"

### Test Scenario 2: Waiting Screen

1. **Open Second Browser** (or incognito window):
   - Navigate to http://localhost:3000/login
   - Login as participant or coach
   - Verify waiting screen appears immediately
   - Verify falling stars animation
   - Verify "Waiting for hack to start..." message
   - Verify animated ellipsis

2. **Keep Both Windows Open**

### Test Scenario 3: Launch Hack

1. **Back in Tech Lead Window**:
   - Click "Launch Hack" button
   - Verify confirmation dialog appears
   - Verify dialog shows:
     - Number of teams configured
     - Number of coaches assigned
     - Content path
   - Click "Launch Now"

2. **Observe Participant Window**:
   - Waiting screen should disappear instantly (no refresh needed)
   - Dashboard or challenges page should appear

3. **Back in Tech Lead Window**:
   - Navigate to "Dashboard"
   - Verify global hack timer appears at top
   - Verify timer is ticking (updates every second)
   - Verify "Hack Started At" shows timestamp
   - Verify "Global Elapsed Time" shows HH:MM:SS format

### Test Scenario 4: Real-time Timer Synchronization

1. **In Tech Lead Dashboard**:
   - Watch the global timer for 10 seconds
   - Verify it increments: 00:00:01, 00:00:02, ..., 00:00:10
   - Verify no page refresh is needed

2. **Open Third Browser Window**:
   - Login as another participant
   - Navigate to Dashboard (if accessible)
   - Verify timer shows same elapsed time
   - Wait 5 seconds on both windows
   - Verify both timers remain synchronized (±1 second)

### Test Scenario 5: Persistence

1. **Stop Backend** (Ctrl+C in API terminal)
2. **Start Backend Again**: `dotnet run`
3. **Refresh Tech Lead Browser**
4. **Verify**:
   - Hack state is still "active"
   - Configuration (teams/coaches) is preserved
   - Timer continues from correct start time (no reset)

## API Testing

### Get Current State (Public Endpoint)

```bash
curl http://localhost:5001/api/hack/state
```

Expected response:
```json
{
  "status": "active",
  "startedAt": "2026-02-17T20:00:00Z",
  "configuredBy": "techlead",
  "updatedAt": "2026-02-17T20:00:05Z"
}
```

### Get Configuration (Requires Auth)

```bash
curl -H "Cookie: auth-session=..." http://localhost:5001/api/hack/config
```

Expected response:
```json
{
  "contentPath": "hackcontent",
  "teams": [
    {
      "name": "Team Alpha",
      "members": ["Alice", "Bob"]
    }
  ],
  "coaches": ["Coach Smith"],
  "updatedAt": "2026-02-17T19:55:00Z"
}
```

## Known Limitations / Future Work

1. **Content Selection**: Currently only supports content path string, not granular challenge selection
2. **Team-specific Timers**: All teams use global start time (no per-team start times)
3. **Pause/Resume**: No ability to pause the hack once started
4. **Completion State**: "completed" state exists but no UI triggers it yet
5. **Undo Launch**: No way to revert hack to waiting state after launch
6. **E2E Tests**: Automated Playwright tests not yet created

## Conclusion

✅ **All requirements from the problem statement have been successfully implemented**:
- Configuration screen for tech lead
- Waiting screen for participants (splash screen style)
- Launch functionality with instant broadcast
- Real-time ticking timers without refresh
- Client-side timer calculation from startedAt

The implementation is production-ready for the described use case, with clean architecture, proper error handling, and support for multiple storage backends.
