import { expect, test } from '@playwright/test';
import { consoleGuard } from '../support/console-guard.js';
import { Api, EDITOR_GROUP, confirmModal, env, login, openUserActions, runId, toasts } from '../support/backoffice.js';

test.describe('UC1 offboard a leaver', () => {
  test('archive from the editor with a reason', async ({ page }) => {
    const guard = consoleGuard(page);
    await login(page, env.adminEmail, env.adminPassword);
    const api = new Api(page);
    const jo = await api.createUser(`Jo ${runId}`);

    expect(await openUserActions(page, jo)).toContain('Archive');
    await page.locator('uui-menu-item[label="Archive"]').click();
    const modal = confirmModal(page);
    await expect(modal.getByText('Do not record sensitive personal details. Use a ticket reference.')).toBeVisible();
    await modal.locator('uui-textarea#reason textarea').fill('Left 24 Oct, SUP-123');
    await modal.locator('uui-button#confirm').click();
    expect(await toasts(page)).toContain('1 archived');

    expect(await api.listedInUsers(jo, `Jo ${runId}`)).toBe(false);
    const record = await api.call<{ reason: string; archivedBy: { name: string } }>('GET', `archived-users/${jo}`);
    expect(record.json.reason).toBe('Left 24 Oct, SUP-123');
    expect(record.json.archivedBy.name).toBe('Demo Admin');

    await page.goto('/umbraco/section/user-management/workspace/archived-users-root');
    await page.locator('archived-users-archived-user-table #search input').fill(`Jo ${runId}`);
    await expect(page.locator('archived-users-archived-user-table uui-table-row')).toHaveCount(1);
    await expect(page.locator('archived-users-archived-user-table').getByText('Left 24 Oct, SUP-123')).toBeVisible();
    await api.unarchive([jo]);
    guard.assertNoErrors();
  });

  test('a hostile reason renders as text (Review Focus 2)', async ({ page }) => {
    const guard = consoleGuard(page);
    await login(page, env.adminEmail, env.adminPassword);
    const api = new Api(page);
    const id = await api.createUser(`Xr ${runId}`);
    await api.archive([id], '<img src=x onerror=window.__xss=1>');

    await page.goto('/umbraco/section/user-management/workspace/archived-users-root');
    await page.locator('archived-users-archived-user-table #search input').fill(`Xr ${runId}`);
    await expect(page.locator('archived-users-archived-user-table uui-table-row')).toHaveCount(1);
    await expect(page.locator('archived-users-archived-user-table').getByText('<img src=x onerror=window.__xss=1>')).toBeVisible();
    expect(await page.evaluate(() => (window as any).__xss)).toBeUndefined();
    await api.unarchive([id]);
    guard.assertNoErrors();
  });

  test('D6 hint on a disabled user who never logged in', async ({ page }) => {
    const guard = consoleGuard(page);
    await login(page, env.adminEmail, env.adminPassword);
    const api = new Api(page);
    const id = await api.createUser(`Never ${runId}`);
    await api.disable(id);

    await openUserActions(page, id);
    await page.locator('uui-menu-item[label="Archive"]').click();
    await expect(confirmModal(page).getByText('This user has never logged in. You can delete them with the core Delete action instead.')).toBeVisible();
    await confirmModal(page).getByRole('button', { name: 'Cancel' }).click();
    guard.assertNoErrors();
  });

  test('no Archive on your own user', async ({ page }) => {
    const guard = consoleGuard(page);
    await login(page, env.adminEmail, env.adminPassword);
    const api = new Api(page);
    const me = (await api.call<{ id: string }>('GET', 'user/current')).json.id;
    expect(await openUserActions(page, me)).not.toContain('Archive');
    guard.assertNoErrors();
  });

  test('no Archive on an invited user', async ({ page }) => {
    const guard = consoleGuard(page);
    await login(page, env.adminEmail, env.adminPassword);
    const api = new Api(page);

    const invite = await api.call('POST', 'user/invite', {
      email: `${runId}.invited@example.test`, userName: `${runId}.invited@example.test`, name: `Invited ${runId}`,
      userGroupIds: [{ id: EDITOR_GROUP }], message: 'e2e',
    });
    // The demo writes invitation emails to a pickup folder (appsettings.json Smtp), so core can always invite here.
    expect(invite.status, `invite the user: ${JSON.stringify(invite.json)}`).toBe(201);
    const invitedId = invite.headers['umb-generated-resource'] ?? invite.headers['location']?.split('/').pop();
    expect(await openUserActions(page, invitedId!)).not.toContain('Archive');
    guard.assertNoErrors();
  });

  test('the Archive disabled users button is absent in a group picker modal', async ({ page }) => {
    const guard = consoleGuard(page);
    await login(page, env.adminEmail, env.adminPassword);
    await page.goto('/umbraco/section/user-management/workspace/user-group/edit/44dc260e-b4d4-4dd9-9081-eec5598f1641');
    const choose = page.locator('#btn-add');
    await choose.last().scrollIntoViewIfNeeded();
    await choose.last().click();
    await expect(page.locator('umb-collection').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Archive disabled users' })).toHaveCount(0);
    guard.assertNoErrors();
  });
});
