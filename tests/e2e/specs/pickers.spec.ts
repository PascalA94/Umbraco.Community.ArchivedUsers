import { expect, test, type Page } from '@playwright/test';
import { consoleGuard } from '../support/console-guard.js';
import { Api, env, login, runId } from '../support/backoffice.js';

// P7, U5: every picker found lists users through core's GET filter/user (docs/spikes.md U5), so an archived user
// is never offered. If U5 had found a surface that doesn't, this spec would assert the user IS offered there.

async function pickerOffers(page: Page, name: string): Promise<boolean> {
  const search = page.locator('umb-modal-sidebar uui-input input, uui-modal-sidebar uui-input input').first();
  await search.fill(name);
  await page.waitForTimeout(1500);
  return (await page.locator('uui-modal-sidebar, umb-modal-sidebar').getByText(name, { exact: true }).count()) > 0;
}

test('a user group "Choose" picker does not offer an archived user', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  const api = new Api(page);
  const archived = await api.createUser(`Pick Gone ${runId}`);
  await api.createUser(`Pick Here ${runId}`);
  await api.archive([archived]);

  await page.goto('/umbraco/section/user-management/workspace/user-group/edit/44dc260e-b4d4-4dd9-9081-eec5598f1641');
  const choose = page.locator('#btn-add');
  await choose.last().scrollIntoViewIfNeeded();
  await choose.last().click();
  expect(await pickerOffers(page, `Pick Here ${runId}`)).toBe(true);
  expect(await pickerOffers(page, `Pick Gone ${runId}`)).toBe(false);

  await api.unarchive([archived]);
  guard.assertNoErrors();
});

test('the User Picker property editor in Content does not offer an archived user', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  const api = new Api(page);
  const archived = await api.createUser(`Content Gone ${runId}`);
  await api.createUser(`Content Here ${runId}`);
  await api.archive([archived]);

  const dataType = await api.call('POST', 'data-type', {
    name: `User picker ${runId}`, editorAlias: 'Umbraco.UserPicker', editorUiAlias: 'Umb.PropertyEditorUi.UserPicker', values: [],
  });
  expect(dataType.status, JSON.stringify(dataType.json)).toBe(201);
  const dataTypeId = dataType.headers['umb-generated-resource'];

  const containerId = crypto.randomUUID();
  const docType = await api.call('POST', 'document-type', {
    alias: `pickerPage${runId}`, name: `Picker page ${runId}`, icon: 'icon-document', allowedAsRoot: true,
    variesByCulture: false, variesBySegment: false, isElement: false, allowedInLibrary: false,
    allowedTemplates: [], allowedDocumentTypes: [], compositions: [], cleanup: { preventCleanup: false },
    containers: [{ id: containerId, name: 'Content', type: 'Tab', sortOrder: 0 }],
    properties: [{
      id: crypto.randomUUID(), container: { id: containerId }, sortOrder: 0, alias: 'owner', name: 'Owner',
      dataType: { id: dataTypeId }, variesByCulture: false, variesBySegment: false,
      validation: { mandatory: false }, appearance: { labelOnTop: false },
    }],
  });
  expect(docType.status, JSON.stringify(docType.json)).toBe(201);
  const docTypeId = docType.headers['umb-generated-resource'];

  const doc = await api.call('POST', 'document', {
    template: null, documentType: { id: docTypeId }, values: [], variants: [{ culture: null, segment: null, name: `Picker doc ${runId}` }],
  });
  expect(doc.status, JSON.stringify(doc.json)).toBe(201);
  const docId = doc.headers['umb-generated-resource'];

  await page.goto(`/umbraco/section/content/workspace/document/edit/${docId}`);
  const add = page.locator('umb-input-user #btn-add, umb-property[alias="owner"] #btn-add, umb-property-editor-ui-user-picker uui-button').first();
  await add.scrollIntoViewIfNeeded();
  await add.click();
  expect(await pickerOffers(page, `Content Here ${runId}`)).toBe(true);
  expect(await pickerOffers(page, `Content Gone ${runId}`)).toBe(false);

  await page.keyboard.press('Escape');
  await api.call('DELETE', `document/${docId}`);
  await api.call('DELETE', `document-type/${docTypeId}`);
  await api.call('DELETE', `data-type/${dataTypeId}`);
  await api.unarchive([archived]);
  guard.assertNoErrors();
});
