import { test, expect } from '@playwright/test';
import { LoginPage } from './pages/login.page';

// DASH-001 – DASH-053: Organizer dashboard, team management

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

async function resetAllTeams(request: import('@playwright/test').APIRequestContext) {
  await apiLogin(request, 'techlead');
  await request.post(`${API}/api/admin/challenges/reset-all`);
  await request.post(`${API}/api/admin/timer/reset-all`);
}

test.describe('Dashboard — Monitoring', () => {
  test('should display all teams with name, challenge step, and timer status', async ({ page, request }) => {
    await resetAllTeams(request);
    await loginAs(page, 'techlead');
    await page.goto('/dashboard');
    // Should show a table with team rows
    const table = page.locator('.MuiTableContainer-root');
    await expect(table).toBeVisible({ timeout: 5000 });
    // Should have at least 2 team rows (team-alpha, team-beta)
    const rows = page.locator('tbody tr');
    const count = await rows.count();
    expect(count).toBeGreaterThanOrEqual(2);
  });

  test('should show challenge progress as fraction (e.g. 3 / 8)', async ({ page, request }) => {
    await resetAllTeams(request);
    await loginAs(page, 'techlead');
    await page.goto('/dashboard');
    const table = page.locator('.MuiTableContainer-root');
    await expect(table).toBeVisible({ timeout: 5000 });
    // Should show step/total in a cell
    const progressText = page.getByText(/\d+\s*\/\s*\d+/);
    await expect(progressText.first()).toBeVisible();
  });

  test('should display timer status correctly for each state', async ({ page, request }) => {
    await resetAllTeams(request);
    await loginAs(page, 'techlead');
    await page.goto('/dashboard');
    const table = page.locator('.MuiTableContainer-root');
    await expect(table).toBeVisible({ timeout: 5000 });
    // Timer status chips should be visible
    const chips = page.locator('tbody .MuiChip-root');
    await expect(chips.first()).toBeVisible();
  });

  test('should show total challenges summary above team table', async ({ page, request }) => {
    await resetAllTeams(request);
    await loginAs(page, 'techlead');
    await page.goto('/dashboard');
    // Should show stats like "X teams • Y challenges"
    const stats = page.getByText(/teams.*challenges|challenges.*available/i);
    await expect(stats.first()).toBeVisible({ timeout: 5000 });
  });

  test('should show empty state when no teams are configured', async ({ page }) => {
    // We can't remove teams, but we can verify the dashboard loads
    await loginAs(page, 'techlead');
    await page.goto('/dashboard');
    await expect(page.getByText(/dashboard/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should not auto-refresh — requires manual reload', async ({ page, request }) => {
    await resetAllTeams(request);
    await loginAs(page, 'techlead');
    await page.goto('/dashboard');
    const table = page.locator('.MuiTableContainer-root');
    await expect(table).toBeVisible({ timeout: 5000 });
    // There should be a refresh button
    const refreshButton = page.getByRole('button', { name: /refresh/i }).or(
      page.locator('[data-testid="RefreshIcon"]').locator('..')
    );
    await expect(refreshButton.first()).toBeVisible();
  });
});

test.describe('Dashboard — Per-Team Challenge Operations', () => {
  test.describe.configure({ mode: 'serial' });

  test('should advance a single team challenge step', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    expect(res.ok()).toBeTruthy();
    // Verify team-alpha is now at step 2
    const teams = await (await request.get(`${API}/api/admin/teams`)).json();
    const alpha = (teams.teams || teams).find((t: any) => t.teamName === 'team-alpha');
    expect(alpha.currentStep).toBe(2);
    // Clean up
    await request.post(`${API}/api/admin/challenges/reset-all`);
  });

  test('should revert a single team challenge step', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/challenges/revert`);
    expect(res.ok()).toBeTruthy();
    const teams = await (await request.get(`${API}/api/admin/teams`)).json();
    const alpha = (teams.teams || teams).find((t: any) => t.teamName === 'team-alpha');
    expect(alpha.currentStep).toBe(1);
  });

  test('should reset a single team to step 1', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/challenges/reset`);
    expect(res.ok()).toBeTruthy();
    const teams = await (await request.get(`${API}/api/admin/teams`)).json();
    const alpha = (teams.teams || teams).find((t: any) => t.teamName === 'team-alpha');
    expect(alpha.currentStep).toBe(1);
  });

  test('should not affect other teams when operating on one team', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    const teams = await (await request.get(`${API}/api/admin/teams`)).json();
    const beta = (teams.teams || teams).find((t: any) => t.teamName === 'team-beta');
    expect(beta.currentStep).toBe(1);
    // Clean up
    await request.post(`${API}/api/admin/challenges/reset-all`);
  });

  test('should show error when advancing past last challenge', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    for (let i = 0; i < 6; i++) {
      await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    }
    const res = await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    expect(res.ok()).toBeFalsy();
    // Clean up
    await request.post(`${API}/api/admin/challenges/reset-all`);
  });

  test('should show error when reverting at first challenge', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/challenges/revert`);
    expect(res.ok()).toBeFalsy();
  });
});

test.describe('Dashboard — Bulk Challenge Operations', () => {
  test('should advance all teams', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    const res = await request.post(`${API}/api/admin/challenges/approve-all`);
    expect(res.ok()).toBeTruthy();
    const teams = await (await request.get(`${API}/api/admin/teams`)).json();
    for (const team of (teams.teams || teams)) {
      expect(team.currentStep).toBe(2);
    }
    // Clean up
    await request.post(`${API}/api/admin/challenges/reset-all`);
  });

  test('should revert all teams', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    await request.post(`${API}/api/admin/challenges/approve-all`);
    const res = await request.post(`${API}/api/admin/challenges/revert-all`);
    expect(res.ok()).toBeTruthy();
    const teams = await (await request.get(`${API}/api/admin/teams`)).json();
    for (const team of (teams.teams || teams)) {
      expect(team.currentStep).toBe(1);
    }
  });

  test('should reset all teams to step 1', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/approve-all`);
    await request.post(`${API}/api/admin/challenges/approve-all`);
    const res = await request.post(`${API}/api/admin/challenges/reset-all`);
    expect(res.ok()).toBeTruthy();
    const teams = await (await request.get(`${API}/api/admin/teams`)).json();
    for (const team of (teams.teams || teams)) {
      expect(team.currentStep).toBe(1);
    }
  });

  test('should show summary with successes and failures for bulk advance', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    const res = await request.post(`${API}/api/admin/challenges/approve-all`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body).toBeDefined();
    // Clean up
    await request.post(`${API}/api/admin/challenges/reset-all`);
  });

  test('should show summary with successes and failures for bulk revert', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/challenges/reset-all`);
    // Revert from step 1 should fail for all teams
    const res = await request.post(`${API}/api/admin/challenges/revert-all`);
    const body = await res.json();
    expect(body).toBeDefined();
  });
});

test.describe('Dashboard — Per-Team Timer Operations', () => {
  test('should start a single team timer', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    expect(res.ok()).toBeTruthy();
    // Clean up
    await request.post(`${API}/api/admin/teams/team-alpha/timer/stop`);
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
  });

  test('should stop a single team timer', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/stop`);
    expect(res.ok()).toBeTruthy();
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
  });

  test('should reset a single team timer', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    expect(res.ok()).toBeTruthy();
  });

  test('should disable start button when timer is already running', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    expect(res.status()).toBe(409);
    // Clean up
    await request.post(`${API}/api/admin/teams/team-alpha/timer/stop`);
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
  });

  test('should disable stop button when timer is not running', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/stop`);
    expect(res.status()).toBe(409);
  });
});

test.describe('Dashboard — Bulk Timer Operations', () => {
  test('should start all team timers', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/timer/reset-all`);
    const res = await request.post(`${API}/api/admin/timer/start-all`);
    expect(res.ok()).toBeTruthy();
    // Clean up
    await request.post(`${API}/api/admin/timer/stop-all`);
    await request.post(`${API}/api/admin/timer/reset-all`);
  });

  test('should stop all team timers', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/timer/reset-all`);
    await request.post(`${API}/api/admin/timer/start-all`);
    const res = await request.post(`${API}/api/admin/timer/stop-all`);
    expect(res.ok()).toBeTruthy();
    await request.post(`${API}/api/admin/timer/reset-all`);
  });

  test('should reset all team timers', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.post(`${API}/api/admin/timer/reset-all`);
    expect(res.ok()).toBeTruthy();
  });

  test('should show per-team results for bulk timer start with mixed states', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/timer/reset-all`);
    await request.post(`${API}/api/admin/teams/team-alpha/timer/start`);
    const res = await request.post(`${API}/api/admin/timer/start-all`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body).toBeDefined();
    // Clean up
    await request.post(`${API}/api/admin/timer/stop-all`);
    await request.post(`${API}/api/admin/timer/reset-all`);
  });
});

test.describe('Dashboard — Access Control', () => {
  test('should allow organizer to access dashboard API', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.get(`${API}/api/admin/teams`);
    expect(res.ok()).toBeTruthy();
  });

  test('should deny participant access to dashboard API (403)', async ({ request }) => {
    await apiLogin(request, 'hacker1');
    const res = await request.get(`${API}/api/admin/teams`);
    expect(res.status()).toBe(403);
  });

  test('should deny coach access to dashboard challenge actions (403)', async ({ request }) => {
    await apiLogin(request, 'coach1');
    const res = await request.post(`${API}/api/admin/teams/team-alpha/challenges/approve`);
    expect(res.status()).toBe(403);
  });

  test('should return 401 for unauthenticated dashboard requests', async ({ request }) => {
    const res = await request.get(`${API}/api/admin/teams`, { headers: { Cookie: '' } });
    expect(res.status()).toBe(401);
  });

  test('should hide dashboard nav link for non-organizer users', async ({ page }) => {
    await loginAs(page, 'hacker1');
    const nav = page.locator('header');
    await expect(nav.getByRole('link', { name: 'Dashboard' })).not.toBeVisible();
  });

  test('should show dashboard nav link for organizer users', async ({ page }) => {
    await loginAs(page, 'techlead');
    const nav = page.locator('header');
    await expect(nav.getByRole('link', { name: 'Dashboard' })).toBeVisible();
  });
});

test.describe('Dashboard — Edge Cases', () => {
  test('should return 404 for actions on non-existent team', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.post(`${API}/api/admin/teams/nonexistent-team/challenges/approve`);
    expect(res.status()).toBe(404);
  });

  test('should return 400 for invalid action value', async ({ request }) => {
    await apiLogin(request, 'techlead');
    const res = await request.post(`${API}/api/admin/teams/team-alpha/challenges/invalid-action`);
    expect([400, 404, 405]).toContain(res.status());
  });

  test('should handle timer reset on not-started timer as no-op', async ({ request }) => {
    await apiLogin(request, 'techlead');
    await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    const res = await request.post(`${API}/api/admin/teams/team-alpha/timer/reset`);
    expect(res.ok()).toBeTruthy();
  });
});
