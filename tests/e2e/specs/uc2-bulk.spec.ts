import { expect, test } from '@playwright/test';
import { consoleGuard } from '../support/console-guard.js';
import { Api, env, login, newSession, runId, toasts } from '../support/backoffice.js';

// P8. The admin case can't be selected in the UI (core hides admins from non-admins), so a non-admin archiving an
// admin is covered by the API test Non_admin_mixed_bulk_returns_per_user_statuses instead.
test('UC2 bulk archive as a non-admin user manager: self is skipped', async ({ page, browser }) => {
  const admin = await newSession(browser, env.adminEmail, env.adminPassword);
  const editorId = await admin.api.createUser(`Bulk Ed ${runId}`);

  const guard = consoleGuard(page);
  await login(page, env.managerEmail, env.managerPassword);
  const api = new Api(page);
  await page.goto('/umbraco/section/user-management/workspace/user-root');
  for (const name of ['Morgan Manager', `Bulk Ed ${runId}`]) {
    await page.locator('uui-input input').first().fill(name);
    await page.waitForTimeout(1200);
    const card = page.locator('uui-card-user', { hasText: name }).first();
    await card.hover();
    await card.locator('#select-checkbox, uui-checkbox').first().click({ force: true });
  }

  await page.getByRole('button', { name: 'Archive', exact: true }).first().click();
  await page.locator('uui-button#confirm').click();
  const text = await toasts(page);
  expect(text).toContain('1 archived, 1 skipped');
  expect(text).toContain("you can't archive yourself");
  expect(await api.isArchived(editorId)).toBe(true);

  await admin.api.unarchive([editorId]);
  await admin.context.close();
  guard.assertNoErrors();
});
