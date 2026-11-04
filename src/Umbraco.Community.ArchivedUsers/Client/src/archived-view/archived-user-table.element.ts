import type { ArchivedUserCollectionItem } from './map-item.js';
import type { ArchivedUserCollectionFilter } from './archived-user-collection.repository.js';
import { debounce } from './debounce.js';
import './archived-users-name-column-layout.element.js';
import { css, customElement, html, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';
import type { UmbDefaultCollectionContext } from '@umbraco-cms/backoffice/collection';
import type { UmbTableColumn, UmbTableElement, UmbTableItem } from '@umbraco-cms/backoffice/components';

type ArchivedCollection = UmbDefaultCollectionContext<ArchivedUserCollectionItem, ArchivedUserCollectionFilter>;

const SORTABLE: Record<string, ArchivedUserCollectionFilter['orderBy']> = { name: 'Name', archived: 'ArchivedUtc' };

/**
 * F4 table: name, email, archived, archived by, reason, last login. Sortable by name and archived date; the search
 * box is debounced 300 ms (AU-13). Every cell is a text binding (AU-04). Built on umb-table because core's table
 * view kind has no sortable headers or custom cells (docs/spikes.md S6).
 */
@customElement('archived-users-archived-user-table')
export class ArchivedUsersArchivedUserTableElement extends UmbLitElement {
  @state() private _items: ArchivedUserCollectionItem[] = [];
  @state() private _selection: string[] = [];
  @state() private _loaded = false;

  #collection?: ArchivedCollection;
  #search = debounce((text: string) => this.#applyFilter({ filter: text }), 300);

  constructor() {
    super();
    this.consumeContext(UMB_COLLECTION_CONTEXT, (collection) => {
      this.#collection = collection as unknown as ArchivedCollection;
      this.observe(collection?.items, (items) => {
        this._items = (items as ArchivedUserCollectionItem[] | undefined) ?? [];
        this._loaded = true;
      }, '_items');
      this.observe(collection?.selection.selection, (selection) => (this._selection = (selection ?? []) as string[]), '_selection');
    });
  }

  #columns(): UmbTableColumn[] {
    return [
      { name: this.localize.term('archivedUsers_columnName'), alias: 'name', elementName: 'archived-users-name-column-layout', allowSorting: true },
      { name: this.localize.term('archivedUsers_columnEmail'), alias: 'email' },
      { name: this.localize.term('archivedUsers_columnArchived'), alias: 'archived', allowSorting: true },
      { name: this.localize.term('archivedUsers_columnArchivedBy'), alias: 'archivedBy' },
      { name: this.localize.term('archivedUsers_columnReason'), alias: 'reason' },
      { name: this.localize.term('archivedUsers_columnLastLogin'), alias: 'lastLogin' },
    ];
  }

  #rows(): UmbTableItem[] {
    return this._items.map((item) => ({
      id: item.unique,
      icon: 'icon-user',
      entityType: 'user',
      data: [
        { columnAlias: 'name', value: item },
        { columnAlias: 'email', value: item.email },
        { columnAlias: 'archived', value: this.#date(item.archivedUtc) },
        {
          columnAlias: 'archivedBy',
          value: item.archivedBySystem
            ? this.localize.term('archivedUsers_system')
            : (item.archivedByName ?? this.localize.term('archivedUsers_removedUser')),
        },
        { columnAlias: 'reason', value: item.reason ?? '' },
        { columnAlias: 'lastLogin', value: item.lastLoginDate ? this.#date(item.lastLoginDate) : '' },
      ],
    }));
  }

  #date(value: string) {
    return this.localize.date(value, { dateStyle: 'medium', timeStyle: 'short' });
  }

  #onOrdered(event: Event) {
    const table = event.target as UmbTableElement;
    const orderBy = SORTABLE[table.orderingColumn];
    if (!orderBy) return;
    this.#applyFilter({ orderBy, orderDirection: table.orderingDesc ? 'Descending' : 'Ascending' });
  }

  /** updateFilter only stores the filter; the collection has to be asked to load again. */
  #applyFilter(filter: Partial<ArchivedUserCollectionFilter>) {
    this.#collection?.updateFilter({ ...filter, skip: 0 });
    this.#collection?.loadCollection();
  }

  #onSelection(event: Event) {
    event.stopPropagation();
    this.#collection?.selection.setSelection((event.target as UmbTableElement).selection);
  }

  override render() {
    return html`
      <uui-input
        id="search"
        label=${this.localize.term('archivedUsers_searchLabel')}
        placeholder=${this.localize.term('archivedUsers_searchLabel')}
        @input=${(e: InputEvent) => this.#search(((e.target as HTMLInputElement).value ?? '').trim())}>
        <uui-icon name="icon-search" slot="prepend"></uui-icon>
      </uui-input>
      ${this._loaded && this._items.length === 0
        ? html`<p id="empty">${this.localize.term('archivedUsers_noArchivedUsers')}</p>`
        : html`<umb-table
            .config=${{ allowSelection: true, hideIcon: false }}
            .columns=${this.#columns()}
            .items=${this.#rows()}
            .selection=${this._selection}
            @selected=${this.#onSelection}
            @deselected=${this.#onSelection}
            @ordered=${this.#onOrdered}></umb-table>`}
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
      }
      #search {
        width: 100%;
        max-width: 32rem;
        margin-bottom: var(--uui-size-space-4);
      }
      #empty {
        color: var(--uui-color-text-alt);
      }
    `,
  ];
}

export { ArchivedUsersArchivedUserTableElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'archived-users-archived-user-table': ArchivedUsersArchivedUserTableElement;
  }
}
