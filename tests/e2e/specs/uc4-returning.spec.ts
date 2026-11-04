import { expect, test } from '@playwright/test';
import { consoleGuard } from '../support/console-guard.js';
import { Api, bulkActionAndConfirm, env, login, runId } from '../support/backoffice.js';

test('UC4 a returning contractor comes back disabled, then core Enable restores them', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  const api = new Api(page);
  const id = await api.createUser(`Contractor ${runId}`);
  await api.archive([id], 'Contract ended');

  await page.goto('/umbraco/section/user-management/workspace/archived-users-root');
  await page.locator('archived-users-archived-user-table #search input').fill(`Contractor ${runId}`);
  const row = page.locator('uui-table-row', { hasText: `Contractor ${runId}` }).first();
  await row.hover();
  await row.locator('uui-checkbox').first().click({ force: true });
  await bulkActionAndConfirm(page, 'Unarchive');
  await expect(page.locator('uui-table-row', { hasText: `Contractor ${runId}` })).toHaveCount(0);

  expect(await api.listedInUsers(id, `Contractor ${runId}`)).toBe(true);
  expect((await api.user(id)).state).toBe('Disabled');

  await api.enable(id);
  expect((await api.user(id)).state).not.toBe('Disabled');
  expect(await api.isArchived(id)).toBe(false);
  guard.assertNoErrors();
});
