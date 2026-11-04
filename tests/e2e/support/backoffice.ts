import { expect, type Browser, type Page } from '@playwright/test';

export const env = {
  adminEmail: process.env.ADMIN_EMAIL ?? 'admin@example.test',
  adminPassword: required('ADMIN_PASSWORD'),
  managerEmail: process.env.MANAGER_EMAIL ?? 'morgan@example.test',
  managerPassword: required('MANAGER_PASSWORD'),
  testPassword: required('TEST_USER_PASSWORD'),
};

function required(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Set ${name} in tests/e2e/.env (see .env.example).`);
  return value;
}

export const ADMIN_GROUP = 'e5e7f6c8-7f9c-4b5b-8d5d-9e1e5a4f7e4d';
export const EDITOR_GROUP = '44dc260e-b4d4-4dd9-9081-eec5598f1641';

/** A unique suffix per run, so specs never collide with each other or with earlier runs. */
export const runId = Date.now().toString(36);

export async function login(page: Page, email: string, password: string) {
  await page.goto('/umbraco');
  const user = page.locator('input[name="username"], input[type="email"]').first();
  await user.waitFor();
  await user.fill(email);
  await page.locator('input[type="password"]').first().fill(password);
  await page.keyboard.press('Enter');
  await page.waitForURL(/\/umbraco\/section\//, { timeout: 60_000 });
  // The backoffice keeps booting (manifests, current user) after the redirect; let it settle before API calls.
  await page.waitForLoadState('networkidle');
}

/**
 * Management API calls with a signed-in page's cookies, shaped as the backoffice sends them: the access token
 * travels in its HttpOnly cookie with a placeholder bearer header (docs/spikes.md S0).
 */
export class Api {
  constructor(private readonly page: Page) {}

  async call<T = unknown>(method: string, path: string, body?: unknown): Promise<{ status: number; json: T; headers: Record<string, string> }> {
    // Sent from Node with the page's cookies, so a deliberate 4xx is not logged as a browser console error and a
    // call can't hang on the backoffice's own fetch handling while it boots.
    const res = await this.page.request.fetch('/umbraco/management/api/v1/' + path, {
      method,
      headers: { Authorization: 'Bearer [redacted]', 'Content-Type': 'application/json' },
      data: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await res.text();
    let json: unknown = null;
    try {
      json = text ? JSON.parse(text) : null;
    } catch {
      json = text;
    }
    return { status: res.status(), json: json as T, headers: res.headers() };
  }

  /** Creates a user in the given groups, sets a password, and returns its id. */
  async createUser(name: string, groupIds: string[] = [EDITOR_GROUP], email?: string): Promise<string> {
    const address = email ?? `${runId}.${name.replace(/[^a-z0-9]+/gi, '.').toLowerCase()}@example.test`.replace(/\.+/g, '.');
    const created = await this.call('POST', 'user', { email: address, userName: address, name, userGroupIds: groupIds.map((id) => ({ id })), kind: 'Default' });
    expect(created.status, JSON.stringify(created.json)).toBe(201);
    const id = created.headers['location']?.split('/').pop() ?? created.headers['umb-generated-resource'];
    expect(id).toBeTruthy();
    const changed = await this.call('POST', `user/${id}/change-password`, { newPassword: env.testPassword });
    expect(changed.status, JSON.stringify(changed.json)).toBe(200);
    return id!;
  }

  async disable(id: string) {
    expect((await this.call('POST', 'user/disable', { userIds: [{ id }] })).status).toBe(200);
  }

  async enable(id: string) {
    expect((await this.call('POST', 'user/enable', { userIds: [{ id }] })).status).toBe(200);
  }

  async archive(ids: string[], reason?: string) {
    const r = await this.call<{ id: string; status: string }[]>('POST', 'archived-users/archive', { userIds: ids, reason });
    expect(r.status).toBe(200);
    return r.json;
  }

  async unarchive(ids: string[]) {
    return (await this.call<{ id: string; status: string }[]>('POST', 'archived-users/unarchive', { userIds: ids })).json;
  }

  async user(id: string) {
    return (await this.call<{ id: string; name: string; state: string }>('GET', `user/${id}`)).json;
  }

  /** Via the list route: GET archived-users/{id} answers 404 for "not archived", which the browser logs as an error. */
  async isArchived(id: string) {
    const r = await this.call<{ items: { user: { id: string } }[] }>('GET', 'archived-users?take=1000');
    return r.json.items.some((i) => i.user.id === id);
  }

  async listedInUsers(id: string, filter: string) {
    const r = await this.call<{ items: { id: string }[] }>('GET', `filter/user?filter=${encodeURIComponent(filter)}&take=100`);
    return r.json.items.some((u) => u.id === id);
  }
}

export async function newSession(browser: Browser, email: string, password: string) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await login(page, email, password);
  return { context, page, api: new Api(page) };
}

/** Opens the user editor and its entity action menu; returns the visible action labels. */
export async function openUserActions(page: Page, id: string): Promise<string[]> {
  await page.goto(`/umbraco/section/user-management/workspace/user/edit/${id}`);
  const menu = page.locator('umb-workspace-entity-action-menu');
  // Read only the opened Actions menu: the section sidebar's menu items are visible too. On a slow runner the first
  // click can land before the workspace is ready and open nothing, so click again until the menu shows.
  const items = menu.locator('uui-menu-item:visible');
  await expect(async () => {
    if (!(await items.count())) {
      await menu.locator('uui-button').first().click();
    }
    await expect(items.first()).toBeVisible({ timeout: 5_000 });
  }).toPass({ timeout: 60_000 });
  await page.waitForTimeout(500);
  return items.evaluateAll((els) => els.map((e) => e.getAttribute('label') ?? ''));
}

/** Text of every notification currently shown (core renders the message as text in a shadow root). */
export async function toasts(page: Page): Promise<string> {
  await page.locator('umb-notification-layout-default').first().waitFor();
  return (
    await page
      .locator('umb-notification-layout-default')
      .evaluateAll((els) => els.map((e) => (e.shadowRoot?.textContent ?? '').replace(/\s+/g, ' ').trim()))
  ).join(' || ');
}

export const confirmModal = (page: Page) => page.locator('archived-users-archive-confirm-modal');

/**
 * Clicks a selection-bar bulk action and its confirm button. On a slow runner the first click can land before the
 * action is ready and open nothing, so it retries until the confirm modal is really visible and Confirm is clicked.
 */
export async function bulkActionAndConfirm(page: Page, label: string): Promise<void> {
  const confirm = page.locator('uui-button#confirm').filter({ visible: true });
  await expect(async () => {
    if (!(await confirm.count())) {
      await page.getByRole('button', { name: label, exact: true }).filter({ visible: true }).first().click();
    }
    await expect(confirm).toBeVisible({ timeout: 5_000 });
    // Inside the retry: a redraw can detach the button mid-click. A click that lands closes the modal and ends the
    // retry, so Confirm is never pressed twice.
    await confirm.click({ timeout: 5_000 });
  }).toPass({ timeout: 60_000 });
}

