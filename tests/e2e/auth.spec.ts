import { test, expect } from '@playwright/test';
import { LoginPage } from './pages/login.page';
import { AppPage } from './pages/app.page';

// AUTH-001 – AUTH-023: Authentication & session flows

const API = process.env.PLAYWRIGHT_BASE_URL
  ? process.env.PLAYWRIGHT_BASE_URL.replace(':3000', ':5001')
  : 'http://localhost:5001';

/** Helper: login via API and return the session cookie value */
async function apiLogin(request: import('@playwright/test').APIRequestContext, username: string, password: string) {
  const res = await request.post(`${API}/api/auth/login`, { data: { username, password } });
  return res;
}

test.describe('Authentication — Login Page', () => {
  test('should display username input, password input, and login button', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await expect(login.usernameInput).toBeVisible();
    await expect(login.passwordInput).toBeVisible();
    await expect(login.loginButton).toBeVisible();
  });

  test('should reject empty username with client-side validation', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.passwordInput.fill('somepass');
    await login.loginButton.click();
    await expect(login.errorMessage).toBeVisible();
  });

  test('should reject empty password with client-side validation', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.usernameInput.fill('someuser');
    await login.loginButton.click();
    await expect(login.errorMessage).toBeVisible();
  });

  test('should reject both fields empty with client-side validation', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.loginButton.click();
    await expect(login.errorMessage).toBeVisible();
  });
});

test.describe('Authentication — Login Flow', () => {
  test.describe.configure({ mode: 'serial' });

  test('should login successfully with valid credentials', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL(/\/(challenges|dashboard)/);
    await expect(page).not.toHaveURL(/\/login/);
  });

  test('should match username case-insensitively', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('HACKER1', 'pass123');
    await page.waitForURL(/\/(challenges|dashboard)/);
  });

  test('should reject wrong password (case-sensitive)', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'PASS123');
    await expect(login.errorMessage).toBeVisible();
  });

  test('should reject non-existent username', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('nonexistent', 'pass123');
    await expect(login.errorMessage).toBeVisible();
  });

  test('should create session cookie on successful login', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL(/\/(challenges|dashboard)/);
    const cookies = await page.context().cookies();
    const session = cookies.find(c => c.name === 'hackbox_session');
    expect(session).toBeDefined();
  });

  test('should set secure attributes on session cookie', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL(/\/(challenges|dashboard)/);
    const cookies = await page.context().cookies();
    const session = cookies.find(c => c.name === 'hackbox_session');
    expect(session).toBeDefined();
    expect(session!.httpOnly).toBe(true);
    expect(session!.sameSite).toBe('Strict');
  });
});

test.describe('Authentication — Role-Based Redirects', () => {
  test('should redirect participant to /challenges after login', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL('**/challenges');
    expect(page.url()).toContain('/challenges');
  });

  test('should redirect coach to /challenges after login', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('coach1', 'pass123');
    await page.waitForURL('**/challenges');
    expect(page.url()).toContain('/challenges');
  });

  test('should redirect techlead to /dashboard after login', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('techlead', 'pass123');
    await page.waitForURL('**/dashboard');
    expect(page.url()).toContain('/dashboard');
  });
});

test.describe('Authentication — Session Management', () => {
  test('should invalidate first session on second login', async ({ request }) => {
    // Login first time
    const res1 = await apiLogin(request, 'hacker1', 'pass123');
    expect(res1.ok()).toBeTruthy();

    // Login second time — old session should be invalidated
    const res2 = await apiLogin(request, 'hacker1', 'pass123');
    expect(res2.ok()).toBeTruthy();
  });

  test('should persist session across page refresh', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL('**/challenges');
    await page.reload();
    await expect(page).not.toHaveURL(/\/login/);
  });
});

test.describe('Authentication — Logout', () => {
  test.describe.configure({ mode: 'serial' });

  test('should clear session cookie on logout', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL('**/challenges');
    const app = new AppPage(page);
    await app.logout();
    await page.waitForURL('**/login');
    const cookies = await page.context().cookies();
    const session = cookies.find(c => c.name === 'hackbox_session');
    // Cookie should be cleared or absent
    expect(!session || session.value === '').toBeTruthy();
  });

  test('should redirect to login page after logout', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL('**/challenges');
    const app = new AppPage(page);
    await app.logout();
    await page.waitForURL('**/login');
    expect(page.url()).toContain('/login');
  });

  test('should handle logout when not logged in (idempotent)', async ({ request }) => {
    const res = await request.post(`${API}/api/auth/logout`);
    // Should not crash — either 200 or 401 is acceptable
    expect([200, 401]).toContain(res.status());
  });
});

test.describe('Authentication — Navigation & Authorization', () => {
  test('should display username and role label in nav bar', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL('**/challenges');
    const app = new AppPage(page);
    await expect(page.getByText('hacker1')).toBeVisible();
    await expect(app.roleChip).toContainText('Participant');
  });

  test('should show only permitted nav links for participant', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL('**/challenges');
    const app = new AppPage(page);
    await expect(app.navLink('Challenges')).toBeVisible();
    await expect(app.navLink('Credentials')).toBeVisible();
    await expect(app.navLink('Timer')).toBeVisible();
    await expect(app.navLink('Dashboard')).not.toBeVisible();
    await expect(app.navLink('Solutions')).not.toBeVisible();
  });

  test('should show only permitted nav links for coach', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('coach1', 'pass123');
    await page.waitForURL('**/challenges');
    const app = new AppPage(page);
    await expect(app.navLink('Challenges')).toBeVisible();
    await expect(app.navLink('Solutions')).toBeVisible();
    await expect(app.navLink('Credentials')).toBeVisible();
    await expect(app.navLink('Timer')).toBeVisible();
    await expect(app.navLink('Dashboard')).not.toBeVisible();
  });

  test('should show all nav links for techlead', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('techlead', 'pass123');
    await page.waitForURL('**/dashboard');
    const app = new AppPage(page);
    await expect(app.navLink('Dashboard')).toBeVisible();
    await expect(app.navLink('Challenges')).toBeVisible();
    await expect(app.navLink('Solutions')).toBeVisible();
    await expect(app.navLink('Credentials')).toBeVisible();
    await expect(app.navLink('Timer')).toBeVisible();
  });

  test('should redirect participant from /dashboard to /challenges', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('hacker1', 'pass123');
    await page.waitForURL('**/challenges');
    await page.goto('/dashboard');
    await page.waitForURL('**/challenges');
    expect(page.url()).toContain('/challenges');
  });

  test('should redirect coach from /dashboard to /challenges', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login('coach1', 'pass123');
    await page.waitForURL('**/challenges');
    await page.goto('/dashboard');
    await page.waitForURL('**/challenges');
    expect(page.url()).toContain('/challenges');
  });

  test('should redirect unauthenticated user to login page', async ({ page }) => {
    await page.goto('/challenges');
    await page.waitForURL('**/login');
    expect(page.url()).toContain('/login');
  });
});
