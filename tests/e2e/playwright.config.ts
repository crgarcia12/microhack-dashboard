import path from 'path';
import { defineConfig, devices } from '@playwright/test';

const repoRoot = path.resolve(__dirname, '../..');

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'on',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: process.env.PLAYWRIGHT_BASE_URL ? undefined : [
    {
      command: `dotnet run --project ${path.join(repoRoot, 'src/api')} --urls http://localhost:5001`,
      url: 'http://localhost:5001/health',
      reuseExistingServer: !process.env.CI,
      timeout: 60000,
    },
    {
      command: `cd "${path.join(repoRoot, 'src/web')}" && npx next dev --port 3000`,
      url: 'http://localhost:3000',
      reuseExistingServer: !process.env.CI,
      timeout: 60000,
      env: { API_URL: 'http://localhost:5001' },
    },
  ],
});
