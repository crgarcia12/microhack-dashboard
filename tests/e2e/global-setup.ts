import type { FullConfig } from '@playwright/test';
import { request } from '@playwright/test';

async function waitForApi(apiBaseUrl: string): Promise<void> {
  const context = await request.newContext({ baseURL: apiBaseUrl });
  try {
    for (let attempt = 0; attempt < 60; attempt += 1) {
      try {
        const res = await context.get('/health');
        if (res.ok()) return;
      } catch {
        // Retry until API is up.
      }
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }
  } finally {
    await context.dispose();
  }

  throw new Error(`API was not ready at ${apiBaseUrl}/health`);
}

export default async function globalSetup(config: FullConfig): Promise<void> {
  const configuredBaseUrl = process.env.PLAYWRIGHT_BASE_URL
    ?? String(config.projects[0]?.use?.baseURL ?? 'http://localhost:3000');
  const apiBaseUrl = configuredBaseUrl.replace(':3000', ':5001');

  await waitForApi(apiBaseUrl);

  const context = await request.newContext({ baseURL: apiBaseUrl });
  try {
    const login = await context.post('/api/auth/login', {
      data: { username: 'techlead', password: 'pass123' },
    });
    if (!login.ok()) {
      throw new Error(`Failed to login as techlead. Status: ${login.status()}`);
    }

    const stateRes = await context.get('/api/hack/state');
    if (!stateRes.ok()) {
      throw new Error(`Failed to read hack state. Status: ${stateRes.status()}`);
    }

    const state = (await stateRes.json()) as { status?: string };
    if (state.status !== 'active') {
      const launch = await context.post('/api/hack/launch');
      if (!launch.ok() && launch.status() !== 409) {
        throw new Error(`Failed to launch hack. Status: ${launch.status()}`);
      }
    }
  } finally {
    await context.dispose();
  }
}
