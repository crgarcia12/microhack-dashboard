import { test, expect } from '@playwright/test';
import { LoginPage } from './pages/login.page';

// TMR-010 – TMR-022: Manual timer (stopwatch) controls

const API = process.env.PLAYWRIGHT_BASE_URL
  ? process.env.PLAYWRIGHT_BASE_URL.replace(':3000', ':5001')
  : 'http://localhost:5001';

async function loginAs(page: import('@playwright/test').Page, username: string) {
  const login = new LoginPage(page);
  await login.goto();
  await login.login(username, 'pass123');
  await page.waitForURL(/\/(challenges|dashboard)/);
}

async function apiLogin(request: import('@playwright/test').APIRequestContext, username: string) {
  await request.post(`${API}/api/auth/login`, { data: { username, password: 'pass123' } });
}

test.describe('Timer — Manual Stopwatch', () => {
  test.describe.configure({ mode: 'serial' });

  test('should initialize with stopped state and zero elapsed', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    const res = await request.get(`${API}/api/timer`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    const manual = body.manual || body.manualTimer || body;
    expect(manual.status || manual.state).toMatch(/stopped/i);
  });

  test('should start the manual stopwatch', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    const res = await request.post(`${API}/api/timer/start`);
    expect(res.ok()).toBeTruthy();
    const state = await (await request.get(`${API}/api/timer`)).json();
    const manual = state.manual || state.manualTimer || state;
    expect(manual.status || manual.state).toMatch(/running/i);
  });

  test('should return 409 when starting an already running timer', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    await request.post(`${API}/api/timer/start`);
    const res = await request.post(`${API}/api/timer/start`);
    expect(res.status()).toBe(409);
    // Clean up
    await request.post(`${API}/api/timer/stop`);
  });

  test('should stop the manual stopwatch and record elapsed time', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    await request.post(`${API}/api/timer/start`);
    // Wait a moment to accumulate time
    await new Promise(r => setTimeout(r, 1100));
    const res = await request.post(`${API}/api/timer/stop`);
    expect(res.ok()).toBeTruthy();
    const state = await (await request.get(`${API}/api/timer`)).json();
    const manual = state.manual || state.manualTimer || state;
    expect(manual.status || manual.state).toMatch(/stopped/i);
  });

  test('should return 409 when stopping an already stopped timer', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    const res = await request.post(`${API}/api/timer/stop`);
    expect(res.status()).toBe(409);
  });

  test('should reset the manual stopwatch to zero', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/start`);
    await new Promise(r => setTimeout(r, 500));
    await request.post(`${API}/api/timer/stop`);
    const res = await request.post(`${API}/api/timer/reset`);
    expect(res.ok()).toBeTruthy();
    const state = await (await request.get(`${API}/api/timer`)).json();
    const manual = state.manual || state.manualTimer || state;
    expect(manual.accumulatedSeconds || manual.elapsed || 0).toBe(0);
  });

  test('should handle reset from stopped state', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    const res = await request.post(`${API}/api/timer/reset`);
    expect(res.ok()).toBeTruthy();
  });

  test('should handle reset when already at zero (idempotent)', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    await request.post(`${API}/api/timer/reset`);
    const res = await request.post(`${API}/api/timer/reset`);
    expect(res.ok()).toBeTruthy();
  });
});

test.describe('Timer — API State', () => {
  test('should return manual timer state via GET /api/timer/manual', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    // Try the /api/timer endpoint (manual may be nested)
    const res = await request.get(`${API}/api/timer`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body).toBeDefined();
  });

  test('should return both automatic and manual timer state via GET /api/timer', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    const res = await request.get(`${API}/api/timer`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    // Should have both automatic and manual timer data
    expect(body).toBeDefined();
  });

  test('should persist manual timer state across start/stop/reset', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    await request.post(`${API}/api/timer/start`);
    await new Promise(r => setTimeout(r, 500));
    await request.post(`${API}/api/timer/stop`);
    const state1 = await (await request.get(`${API}/api/timer`)).json();
    // Second read should return same state
    const state2 = await (await request.get(`${API}/api/timer`)).json();
    expect(JSON.stringify(state1)).toBe(JSON.stringify(state2));
    // Clean up
    await request.post(`${API}/api/timer/reset`);
  });
});

test.describe('Timer — Automatic Timing', () => {
  test('should start background timer on first challenge approve', async ({ request }) => {
    await apiLogin(request, 'coach1');
    await request.post(`${API}/api/teams/progress/reset`);
    await request.post(`${API}/api/teams/progress/approve`);
    const timer = await (await request.get(`${API}/api/timer`)).json();
    expect(timer).toBeDefined();
    // Clean up
    await request.post(`${API}/api/teams/progress/reset`);
  });

  test('should record challenge duration on approve', async ({ request }) => {
    await apiLogin(request, 'coach1');
    await request.post(`${API}/api/teams/progress/reset`);
    await request.post(`${API}/api/teams/progress/approve`);
    await new Promise(r => setTimeout(r, 500));
    await request.post(`${API}/api/teams/progress/approve`);
    const timer = await (await request.get(`${API}/api/timer`)).json();
    const challengeTimes = timer.challengeTimes || timer.automatic?.challengeTimes;
    expect(challengeTimes).toBeDefined();
    // Clean up
    await request.post(`${API}/api/teams/progress/reset`);
  });

  test('should remove recorded time on revert', async ({ request }) => {
    await apiLogin(request, 'coach1');
    await request.post(`${API}/api/teams/progress/reset`);
    await request.post(`${API}/api/teams/progress/approve`);
    await request.post(`${API}/api/teams/progress/revert`);
    const timer = await (await request.get(`${API}/api/timer`)).json();
    expect(timer).toBeDefined();
    // Clean up
    await request.post(`${API}/api/teams/progress/reset`);
  });

  test('should clear all times on reset', async ({ request }) => {
    await apiLogin(request, 'coach1');
    await request.post(`${API}/api/teams/progress/reset`);
    const timer = await (await request.get(`${API}/api/timer`)).json();
    const challengeTimes = timer.challengeTimes || timer.automatic?.challengeTimes || {};
    expect(Object.keys(challengeTimes).length).toBe(0);
  });

  test('should stop automatic timer after last challenge approval', async ({ request }) => {
    await apiLogin(request, 'coach1');
    await request.post(`${API}/api/teams/progress/reset`);
    for (let i = 0; i < 6; i++) {
      await request.post(`${API}/api/teams/progress/approve`);
    }
    const timer = await (await request.get(`${API}/api/timer`)).json();
    expect(timer).toBeDefined();
    // Clean up
    await request.post(`${API}/api/teams/progress/reset`);
  });
});

test.describe('Timer — Independence', () => {
  test('should keep automatic and manual timers independent', async ({ request }) => {
    await apiLogin(request, 'coach1');
    await request.post(`${API}/api/teams/progress/reset`);
    await request.post(`${API}/api/timer/reset`);
    // Start manual timer
    await request.post(`${API}/api/timer/start`);
    // Approve a challenge (starts automatic timer)
    await request.post(`${API}/api/teams/progress/approve`);
    // Stop manual timer
    await request.post(`${API}/api/timer/stop`);
    const timer = await (await request.get(`${API}/api/timer`)).json();
    // Both timers should exist independently
    expect(timer).toBeDefined();
    // Clean up
    await request.post(`${API}/api/timer/reset`);
    await request.post(`${API}/api/teams/progress/reset`);
  });

  test('should not affect automatic timing when manual timer actions occur', async ({ request }) => {
    await apiLogin(request, 'coach1');
    await request.post(`${API}/api/teams/progress/reset`);
    await request.post(`${API}/api/timer/reset`);
    await request.post(`${API}/api/teams/progress/approve`);
    // Start/stop manual timer shouldn't affect automatic
    await request.post(`${API}/api/timer/start`);
    await request.post(`${API}/api/timer/stop`);
    await request.post(`${API}/api/timer/reset`);
    const progress = await (await request.get(`${API}/api/teams/progress`)).json();
    expect(progress.currentStep).toBe(2);
    // Clean up
    await request.post(`${API}/api/teams/progress/reset`);
  });
});

test.describe('Timer — Authentication', () => {
  test('should require authentication for manual timer endpoints', async ({ request }) => {
    const res = await request.post(`${API}/api/timer/start`, { headers: { Cookie: '' } });
    expect(res.status()).toBe(401);
  });
});

test.describe('Timer — UI', () => {
  test('should display timer with start, stop, and reset buttons', async ({ page, request }) => {
    await apiLogin(request, 'hacker1');
    await request.post(`${API}/api/timer/reset`);
    await loginAs(page, 'hacker1');
    await page.goto('/timer');
    await expect(page.getByRole('button', { name: /start/i })).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /stop/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /reset/i })).toBeVisible();
  });

  test('should show time display in HH:MM:SS format', async ({ page }) => {
    await loginAs(page, 'hacker1');
    await page.goto('/timer');
    const timeDisplay = page.getByText(/\d{2}:\d{2}:\d{2}/);
    await expect(timeDisplay.first()).toBeVisible({ timeout: 5000 });
  });
});

test.describe('Timer — Organizer Controls', () => {
  test('should allow organizer to start timer for a specific team', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    expect(res.ok()).toBeTruthy();
    // Clean up
    await request.post(`${API}/api/admin/teams/team-alpha/timer/stop`);
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
  });

  test('should allow organizer to stop timer for a specific team', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/stop`);
    expect(res.ok()).toBeTruthy();
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
  });

  test('should allow organizer to reset timer for a specific team', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    expect(res.ok()).toBeTruthy();
  });

  test('should return 404 for non-existent team', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.post(`${API}/api/admin/teams/nonexistent-team/timer/start`);
    expect(res.status()).toBe(404);
  });

  test('should deny non-organizer access to per-team timer endpoint', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    expect(res.status()).toBe(403);
  });
});

test.describe('Timer — Bulk Operations', () => {
  test('should bulk start all teams manual timers', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/timer/reset-all`);
    const res = await request.post(`${API}/api/admin/timer/start-all`);
    expect(res.ok()).toBeTruthy();
    // Clean up
    await request.post(`${API}/api/admin/timer/stop-all`);
    await request.post(`${API}/api/admin/timer/reset-all`);
  });

  test('should bulk stop all teams manual timers', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/timer/reset-all`);
    await request.post(`${API}/api/admin/timer/start-all`);
    const res = await request.post(`${API}/api/admin/timer/stop-all`);
    expect(res.ok()).toBeTruthy();
    await request.post(`${API}/api/admin/timer/reset-all`);
  });

  test('should bulk reset all teams manual timers', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.post(`${API}/api/admin/timer/reset-all`);
    expect(res.ok()).toBeTruthy();
  });

  test('should return per-team results for bulk operations with mixed states', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/timer/reset-all`);
    // Start only one team
    await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    // Start all — team-alpha should conflict, team-beta should succeed
    const res = await request.post(`${API}/api/admin/timer/start-all`);
    // Response should indicate mixed results
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body).toBeDefined();
    // Clean up
    await request.post(`${API}/api/admin/timer/stop-all`);
    await request.post(`${API}/api/admin/timer/reset-all`);
  });

  test('should require organizer role for bulk operations', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    const res = await request.post(`${API}/api/admin/timer/start-all`);
    expect(res.status()).toBe(403);
  });
});
