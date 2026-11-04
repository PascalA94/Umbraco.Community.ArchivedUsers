import { expect, test } from '@playwright/test';
import { consoleGuard } from '../support/console-guard.js';
import { Api, confirmModal, env, login, runId, toasts } from '../support/backoffice.js';

test('UC2 archive every disabled user', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  const api = new Api(page);
  const a = await api.createUser(`Gone A ${runId}`);
  await api.disable(a);
  const expected = (await api.call<{ count: number }>('GET', 'archived-users/clean-up/preview')).json.count;
  expect(expected).toBeGreaterThan(0);

  await page.goto('/umbraco/section/user-management/workspace/user-root');
  await page.getByRole('button', { name: 'Archive disabled users' }).click();
  await expect(confirmModal(page).locator('#count')).toHaveText(expected === 1 ? '1 disabled user will be archived.' : `${expected} disabled users will be archived.`);
  await confirmModal(page).locator('uui-button#confirm').click();
  expect(await toasts(page)).toContain(`${expected} archived`);

  expect((await api.call<{ count: number }>('GET', 'archived-users/clean-up/preview')).json.count).toBe(0);
  guard.assertNoErrors();
});
