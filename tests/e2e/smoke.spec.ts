import { test, expect } from '@playwright/test';
import { LoginPage } from './pages/login.page';

// @smoke — Basic smoke tests for deployment verification

const API = process.env.PLAYWRIGHT_API_BASE_URL || (process.env.PLAYWRIGHT_BASE_URL
  ? process.env.PLAYWRIGHT_BASE_URL.replace(':3000', ':5001')
  : 'http://localhost:5001');

test.describe('Smoke Tests @smoke', () => {
  test('homepage loads and shows login form', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await expect(login.usernameInput).toBeVisible();
    await expect(login.passwordInput).toBeVisible();
    await expect(login.loginButton).toBeVisible();
  });

  test('login works with valid credentials', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await expect(page).toHaveURL(/\/challenges/, { timeout: 30000 });
    await expect(page).not.toHaveURL(/\/login/);
  });

  test('API health endpoint responds', async ({ request }) => {
    const res = await request.get(`${API}/health`);
    expect(res.ok()).toBeTruthy();
  });

  test('authenticated user can view challenge list', async ({ request }) => {
    // Login first
    const loginRes = await request.post(`${API}/api/auth/login`, {
      data: { username: 'hacker1', password: 'pass123' },
    });
    expect(loginRes.ok()).toBeTruthy();

    // Fetch challenges
    const res = await request.get(`${API}/api/challenges`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(Array.isArray(body)).toBeTruthy();
    expect(body.length).toBeGreaterThan(0);
  });

  test('team progress endpoint returns valid data', async ({ request }) => {
    await request.post(`${API}/api/auth/login`, {
      data: { username: 'hacker1', password: 'pass123' },
    });
    const res = await request.get(`${API}/api/teams/progress`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body).toHaveProperty('currentStep');
    expect(body).toHaveProperty('totalChallenges');
  });

  test('credentials endpoint returns data for authenticated user', async ({ request }) => {
    await request.post(`${API}/api/auth/login`, {
      data: { username: 'hacker1', password: 'pass123' },
    });
    const res = await request.get(`${API}/api/credentials`);
    expect(res.ok()).toBeTruthy();
  });

  test('unauthenticated request is rejected with 401', async ({ request }) => {
    // Use a fresh context with no cookies
    const res = await request.get(`${API}/api/challenges`, {
      headers: { Cookie: '' },
    });
    expect(res.status()).toBe(401);
  });

  test('coach can access solutions endpoint', async ({ request }) => {
    const loginRes = await request.post(`${API}/api/auth/login`, {
      data: { username: 'coach1', password: 'pass123' },
    });
    expect(loginRes.ok()).toBeTruthy();
    const res = await request.get(`${API}/api/solutions`);
    expect(res.ok()).toBeTruthy();
  });
});
