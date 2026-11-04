import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';

/**
 * Name cell of the Archived users table: the user's name and, for a stale record, a tag (AU-03).
 * A plain LitElement (no backoffice context) so it can be rendered in a unit test. Both values are text
 * bindings; a name is user text and may be hostile (AU-04, P11).
 */
export class ArchivedUserNameCellElement extends LitElement {
  static override properties = {
    name: { type: String },
    stale: { type: Boolean },
    staleLabel: { type: String, attribute: 'stale-label' },
  };

  declare name: string;
  declare stale: boolean;
  declare staleLabel: string;

  constructor() {
    super();
    this.name = '';
    this.stale = false;
    this.staleLabel = '';
  }

  override render() {
    return html`<span class="name">${this.name}</span>${this.stale ? html` <uui-tag color="warning" look="secondary">${this.staleLabel}</uui-tag>` : nothing}`;
  }

  static override styles = css`
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--uui-size-space-2, 6px);
      flex-wrap: wrap;
    }
  `;
}

if (!customElements.get('archived-user-name-cell')) {
  customElements.define('archived-user-name-cell', ArchivedUserNameCellElement);
}

declare global {
  interface HTMLElementTagNameMap {
    'archived-user-name-cell': ArchivedUserNameCellElement;
  }
}
