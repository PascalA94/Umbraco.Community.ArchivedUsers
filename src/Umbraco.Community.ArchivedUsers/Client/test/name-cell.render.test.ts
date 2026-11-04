// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import '../src/archived-view/archived-user-name-cell.element.js';

const HOSTILE = '<img src=x onerror=window.__xss=1>';

async function render(stale: boolean) {
  const el = document.createElement('archived-user-name-cell') as HTMLElement & { name: string; stale: boolean; staleLabel: string; updateComplete: Promise<unknown> };
  el.name = HOSTILE;
  el.stale = stale;
  el.staleLabel = 'Enabled - archive record is stale';
  document.body.appendChild(el);
  await el.updateComplete;
  return el.shadowRoot!;
}

describe('archived-user-name-cell (P11, AU-04)', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('renders a hostile name as text with the stale tag', async () => {
    const root = await render(true);
    expect(root.querySelector('img')).toBeNull();
    expect(root.textContent).toContain(HOSTILE);
    expect(root.querySelector('uui-tag')?.textContent).toContain('Enabled - archive record is stale');
    expect((window as any).__xss).toBeUndefined();
  });

  it('shows no tag when not stale', async () => {
    const root = await render(false);
    expect(root.querySelector('uui-tag')).toBeNull();
    expect(root.querySelector('img')).toBeNull();
  });
});
