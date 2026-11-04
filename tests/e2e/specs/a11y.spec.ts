import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { consoleGuard } from '../support/console-guard.js';
import { Api, confirmModal, env, login, openUserActions, runId } from '../support/backoffice.js';

const TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

/**
 * axe can't include an element inside shadow DOM, so scan the page and keep violations whose node path runs
 * through one of the package's elements (core's own markup is core's to fix).
 */
async function packageViolations(page: Page, tag: string) {
  const results = await new AxeBuilder({ page }).withTags(TAGS).analyze();
  return results.violations
    .map((v) => ({ ...v, nodes: v.nodes.filter((n) => JSON.stringify(n.target).includes(tag)) }))
    .filter((v) => v.nodes.length > 0)
    .map((v) => `${v.id}: ${v.help} (${v.nodes.map((n) => JSON.stringify(n.target)).join(', ')})`);
}

test('the Archive confirm modal meets WCAG 2.2 AA and works by keyboard', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  const api = new Api(page);
  const id = await api.createUser(`A11y ${runId}`);

  await openUserActions(page, id);
  await page.locator('uui-menu-item[label="Archive"]').click();
  await expect(confirmModal(page)).toBeVisible();

  expect(await packageViolations(page, 'archived-users-archive-confirm-modal')).toEqual([]);

  // Keyboard only: Tab reaches the reason field and the confirm button; Escape closes the dialog.
  const reached = new Set<string>();
  for (let i = 0; i < 8; i++) {
    await page.keyboard.press('Tab');
    reached.add(await page.evaluate(() => {
      let el: Element | null = document.activeElement;
      while (el?.shadowRoot?.activeElement) el = el.shadowRoot.activeElement;
      const host = (el?.getRootNode() as ShadowRoot)?.host;
      return [el?.id, el?.tagName, host?.id, host?.tagName].filter(Boolean).join('|').toLowerCase();
    }));
  }
  const all = [...reached].join(' ');
  expect(all).toContain('reason');
  expect(all).toContain('confirm');

  await page.keyboard.press('Escape');
  await expect(confirmModal(page)).toHaveCount(0);
  guard.assertNoErrors();
});

test('the Archived users view meets WCAG 2.2 AA', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  const api = new Api(page);
  const id = await api.createUser(`A11y view ${runId}`);
  await api.archive([id], 'a11y');

  await page.goto('/umbraco/section/user-management/workspace/archived-users-root');
  await expect(page.locator('archived-users-archived-user-table uui-table-row').first()).toBeVisible();
  expect(await packageViolations(page, 'archived-users-')).toEqual([]);

  await api.unarchive([id]);
  guard.assertNoErrors();
});
