import { type Page, type Locator } from '@playwright/test';

export class AppPage {
  readonly page: Page;
  readonly nav: Locator;
  readonly logoutButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.nav = page.locator('header');
    this.logoutButton = page.getByRole('button', { name: /logout/i });
  }

  navLink(name: string): Locator {
    return this.nav.getByRole('link', { name });
  }

  /** The username text shown in the app bar */
  get usernameDisplay(): Locator {
    return this.nav.locator('.MuiToolbar-root > div:last-child').getByText(/.+/, { exact: false }).first();
  }

  /** The role chip shown in the app bar */
  get roleChip(): Locator {
    return this.nav.locator('.MuiChip-root');
  }

  async logout() {
    await this.logoutButton.click();
  }
}
