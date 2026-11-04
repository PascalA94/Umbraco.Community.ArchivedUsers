import type { ArchivedUserCollectionItem } from './map-item.js';
import './archived-user-name-cell.element.js';
import { customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_EDIT_USER_WORKSPACE_PATH_PATTERN } from '@umbraco-cms/backoffice/user';

/** Name column: a link to core's user editor around the name cell (text bindings only, AU-04). */
@customElement('archived-users-name-column-layout')
export class ArchivedUsersNameColumnLayoutElement extends UmbLitElement {
  @property({ attribute: false })
  value?: ArchivedUserCollectionItem;

  override render() {
    if (!this.value) return nothing;
    const href = UMB_EDIT_USER_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique: this.value.unique });
    return html`<a href=${href}
      ><archived-user-name-cell
        .name=${this.value.name}
        .stale=${this.value.stale}
        .staleLabel=${this.localize.term('archivedUsers_staleTag')}></archived-user-name-cell
    ></a>`;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'archived-users-name-column-layout': ArchivedUsersNameColumnLayoutElement;
  }
}
