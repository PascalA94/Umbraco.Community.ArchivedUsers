import { expect, test, type Page } from '@playwright/test';
import { consoleGuard } from '../support/console-guard.js';
import { Api, ADMIN_GROUP, confirmModal, env, login, newSession, openUserActions, runId, toasts } from '../support/backoffice.js';

// T6, AU-04, P11: hostile names and reasons render as text on every package surface.
const IMG = '<img src=x onerror=window.__xss=1>';
const SVG = '"><svg onload=window.__xss=1>';
const PERFORMER = '<img src=x onerror=window.__xss=2>';

async function assertSafe(page: Page, literal: string) {
  await expect(page.getByText(literal).first()).toBeVisible();
  const injected = await page.evaluate(() => {
    const count = (root: Document | ShadowRoot): number =>
      root.querySelectorAll('img[src="x"], svg[onload]').length +
      [...root.querySelectorAll('*')].reduce((n, el) => n + (el.shadowRoot ? count(el.shadowRoot) : 0), 0);
    return count(document);
  });
  expect(injected).toBe(0);
  expect(await page.evaluate(() => (window as any).__xss)).toBeUndefined();
}

test('hostile names, reasons and performer names render as text', async ({ page, browser }) => {
  page.on('dialog', (d) => {
    throw new Error('Unexpected dialog: ' + d.message());
  });
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  const api = new Api(page);

  const hostileA = await api.createUser(IMG + runId);
  const hostileB = await api.createUser(SVG + runId);
  await api.disable(hostileA);
  await api.disable(hostileB);

  // A hostile email: core's own validation decides whether it can exist at all.
  // Sent from Node, not the page, so core's expected 400 isn't logged as a browser console error.
  const email = await page.request.post('/umbraco/management/api/v1/user', {
    headers: { Authorization: 'Bearer [redacted]' },
    data: { email: `"><svg/onload=window.__xss=3>"@example.test`, userName: `x${runId}@example.test`, name: `Email ${runId}`, userGroupIds: [{ id: '44dc260e-b4d4-4dd9-9081-eec5598f1641' }], kind: 'Default' },
  });
  // If core rejects it (400), no package surface can ever receive such an email; record which.
  expect([201, 400]).toContain(email.status());
  test.info().annotations.push({ type: 'hostile-email', description: `core answered ${email.status()}` });

  // Single archive confirm (the user editor) and the post-action toast.
  await openUserActions(page, hostileA);
  await page.locator('uui-menu-item[label="Archive"]').click();
  await confirmModal(page).locator('uui-textarea#reason textarea').fill(IMG);
  await confirmModal(page).locator('uui-button#confirm').click();
  await toasts(page);
  await assertSafe(page, IMG + runId);
  await api.unarchive([hostileA]);

  // "Archive disabled users" with both hostile names in the sample list, then the bulk summary toast.
  await page.goto('/umbraco/section/user-management/workspace/user-root');
  await page.getByRole('button', { name: 'Archive disabled users' }).click();
  await expect(confirmModal(page).locator('#sample li').first()).toBeVisible();
  const sample = await confirmModal(page).locator('#sample li').allInnerTexts();
  test.info().annotations.push({ type: 'sample', description: String(sample.length) });
  await assertSafe(page, sample.find((n) => n.includes(runId)) ?? sample[0]);
  await confirmModal(page).locator('#reason textarea').fill(SVG);
  await confirmModal(page).locator('uui-button#confirm').click();
  await toasts(page);
  expect(await page.evaluate(() => (window as any).__xss)).toBeUndefined();

  // A hostile performer archives a third user, so "archived by" carries the hostile name.
  const performerId = await api.createUser(PERFORMER + runId, [ADMIN_GROUP], `${runId}.performer@example.test`);
  const victim = await api.createUser(`Victim ${runId}`);
  const performer = await newSession(browser, `${runId}.performer@example.test`, env.testPassword);
  await performer.api.archive([victim], 'by hostile performer');
  await performer.context.close();

  // The Archived users table: name, email and "archived by" columns.
  await page.goto('/umbraco/section/user-management/workspace/archived-users-root');
  await page.locator('archived-users-archived-user-table #search input').fill(runId);
  await expect(page.locator('archived-users-archived-user-table uui-table-row').first()).toBeVisible();
  await assertSafe(page, SVG + runId);
  await assertSafe(page, PERFORMER + runId);

  await api.unarchive([hostileB, victim]);
  await api.disable(performerId);
  guard.assertNoErrors();
});
