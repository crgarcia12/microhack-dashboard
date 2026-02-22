# Hack Lifecycle Management

## Overview

This feature implements a complete hack lifecycle management system that allows tech leads to configure and launch microhack events with proper state management and real-time synchronization.

## Architecture

### Backend Components

#### Models
- **HackState**: Tracks the lifecycle status (not_started, configuration, waiting, active, completed)
- **HackConfig**: Stores event configuration (content path, teams, coaches)

#### Entities
- **HackStateEntity**: Database entity for hack state
- **HackConfigEntity**: Database entity for hack configuration (with JSON serialization for complex data)

#### Repositories
- **IHackStateRepository** / **EfHackStateRepository** / **FileHackStateRepository**
- **IHackConfigRepository** / **EfHackConfigRepository** / **FileHackConfigRepository**
- Supports both EF Core (SQL Server/SQLite) and file-based storage

#### Services
- **HackStateService**: Manages state transitions and configuration
  - `GetState()`: Retrieve current hack state
  - `GetConfig()`: Retrieve hack configuration
  - `SaveConfig()`: Save configuration and transition to "waiting" state
  - `LaunchHack()`: Launch the hack and transition to "active" state

#### API Endpoints
- `GET /api/hack/state`: Get current hack state (public - all users)
- `GET /api/hack/config`: Get configuration (tech lead only)
- `POST /api/hack/config`: Save configuration (tech lead only)
- `POST /api/hack/launch`: Launch the hack (tech lead only)

All endpoints integrate with SignalR for real-time updates:
- `hackStateChanged` event: Sent when state changes
- `hackLaunched` event: Sent when hack is launched

### Frontend Components

#### Context Providers
- **HackStateProvider**: Manages hack state and SignalR subscriptions
  - Polls `/api/hack/state` on mount
  - Listens for SignalR events for real-time updates
  - Provides hack state to all authenticated components

#### Pages
- **`/hack-config`**: Configuration page for tech leads
  - Content path configuration
  - Team creation and member assignment
  - Coach assignment
  - Launch button with confirmation dialog
  - Real-time status indicator

#### Components
- **WaitingScreen**: Full-screen overlay shown to non-tech-lead users
  - Reuses splash screen visual design (falling stars, glow effects)
  - Animated "Waiting for hack to start..." message
  - Automatically displayed when hack state is "waiting" or "configuration"

#### Real-time Timers
- Dashboard uses hack's `startedAt` timestamp
- Client-side calculation: `elapsed = currentTime - startedAt`
- Updates every second without API calls
- Global hack timer displayed at top of dashboard
- All team timers sync to same start time

## State Flow

```
not_started → [Tech lead saves config] → waiting → [Tech lead launches] → active
                                                                              ↓
                                                                         completed
```

### State Transitions

1. **not_started**: Initial state on fresh install
2. **waiting**: Tech lead has saved configuration, participants see waiting screen
3. **active**: Tech lead has launched the hack, all users see content and timers start
4. **completed**: (Future) Hack has ended

## User Experience

### Tech Lead
1. Logs in and sees "Config" in navigation (only visible when hack not active)
2. Navigates to `/hack-config`
3. Configures:
   - Content path (default: "hackcontent")
   - Teams (add teams and assign members)
   - Coaches (add coach names)
4. Clicks "Save Configuration" → State becomes "waiting"
5. Clicks "Launch Hack" → State becomes "active", all users notified
6. Dashboard shows:
   - Global hack timer (real-time, updates every second)
   - Individual team progress
   - All team timers sync to global start time

### Coaches and Participants
1. Log in during configuration → See waiting screen with animated message
2. Cannot access challenges or other content until launch
3. When tech lead launches → Waiting screen disappears instantly (SignalR)
4. Can now access all content and timers are running

## Configuration

No additional configuration needed. The system:
- Auto-detects data provider (File, SQLite, or SQL Server)
- Creates tables automatically on first run (EF Core)
- Falls back to file storage in `config-data/` directory
- Uses existing SignalR hub (`/hubs/progress`)

## Testing

### Manual Testing Steps

1. **Fresh Start**:
   ```bash
   # Delete existing state
   rm -f src/api/config-data/hack-state.json
   rm -f src/api/config-data/hack-config.json
   ```

2. **As Tech Lead**:
   - Login as techlead
   - Navigate to Config page
   - Add teams and coaches
   - Save configuration
   - Launch hack

3. **As Participant** (separate browser/incognito):
   - Login as participant/coach
   - Verify waiting screen appears
   - When tech lead launches, verify content appears immediately

4. **Timer Verification**:
   - After launch, verify dashboard timer ticks every second
   - Verify timer doesn't require page refresh
   - Verify all team timers show same elapsed time

### API Testing

```bash
# Get current state
curl http://localhost:5001/api/hack/state

# Save config (tech lead auth required)
curl -X POST http://localhost:5001/api/hack/config \
  -H "Content-Type: application/json" \
  -d '{"teams":[{"name":"Team A","members":["Alice","Bob"]}],"coaches":["Coach1"]}'

# Launch hack (tech lead auth required)
curl -X POST http://localhost:5001/api/hack/launch
```

## Future Enhancements

- [ ] Content selection UI (pick which challenges to include)
- [ ] Team-specific timers (start times per team)
- [ ] Pause/resume functionality
- [ ] Hack completion ceremony/screen
- [ ] Export configuration/results
- [ ] Webhook notifications for external systems
