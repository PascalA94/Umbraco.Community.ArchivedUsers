import { expect, test } from '@playwright/test';
import { env, newSession, runId } from '../support/backoffice.js';

// T4, AU-02, U1, P9: archiving ends the user's session and their last tokens stop working.
test('an archived user is signed out and their tokens are refused', async ({ browser }) => {
  const admin = await newSession(browser, env.adminEmail, env.adminPassword);
  const editorId = await admin.api.createUser(`Session ${runId}`);
  const other = await admin.api.createUser(`Bystander ${runId}`);
  const editorEmail = (await admin.api.call<{ email: string }>('GET', `user/${editorId}`)).json.email;

  const b = await newSession(browser, editorEmail, env.testPassword);
  await b.page.goto('/umbraco');
  expect((await b.api.call('GET', 'user/current')).status).toBe(200);

  await admin.api.archive([editorId], 'Left');

  // (d) U1: the last access token is a reference token, revoked on disable, so it stops at once.
  const current = await b.api.call('GET', 'user/current');
  expect(current.status).toBe(401);

  // (a) A refresh is refused.
  const refresh = await b.page.request.post('/umbraco/management/api/v1/security/back-office/token', {
    form: { grant_type: 'refresh_token', client_id: 'umbraco-back-office', refresh_token: '[redacted]' },
  });
  expect(refresh.status()).not.toBe(200);

  // (e) P9: the archived user's last token can't archive anyone.
  const attempt = await b.api.call<{ id: string; status: string }[]>('POST', 'archived-users/archive', { userIds: [other] });
  expect(attempt.status === 401 || attempt.json?.[0]?.status === 'NotPermitted').toBe(true);
  expect((await admin.api.user(other)).state).not.toBe('Disabled');

  // (b) Reloading the backoffice ends on the login screen.
  await b.page.goto('/umbraco');
  await expect(b.page.locator('input[type="password"]')).toBeVisible({ timeout: 30_000 });

  await admin.api.unarchive([editorId]);
  await b.context.close();
  await admin.context.close();
});
