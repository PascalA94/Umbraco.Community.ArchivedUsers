import { t as e } from "./map-item-BAWaCWpf.js";
import { t } from "./decorate-DsqXQ0nF.js";
import { UMB_COLLECTION_CONTEXT as n } from "@umbraco-cms/backoffice/collection";
import { UMB_EDIT_USER_WORKSPACE_PATH_PATTERN as r } from "@umbraco-cms/backoffice/user";
import { LitElement as i, css as a, customElement as o, html as s, nothing as c, property as l, state as u } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as d } from "@umbraco-cms/backoffice/lit-element";
// src/archived-view/debounce.ts
function f(e, t) {
	let n;
	return (...r) => {
		n !== void 0 && clearTimeout(n), n = setTimeout(() => {
			n = void 0, e(...r);
		}, t);
	};
}
// src/archived-view/archived-user-name-cell.element.ts
var p = class extends i {
	static {
		this.properties = {
			name: { type: String },
			stale: { type: Boolean },
			staleLabel: {
				type: String,
				attribute: "stale-label"
			}
		};
	}
	constructor() {
		super(), this.name = "", this.stale = !1, this.staleLabel = "";
	}
	render() {
		return s`<span class="name">${this.name}</span>${this.stale ? s` <uui-tag color="warning" look="secondary">${this.staleLabel}</uui-tag>` : c}`;
	}
	static {
		this.styles = a`
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--uui-size-space-2, 6px);
      flex-wrap: wrap;
    }
  `;
	}
};
customElements.get("archived-user-name-cell") || customElements.define("archived-user-name-cell", p);
// src/archived-view/archived-users-name-column-layout.element.ts
var m = class extends d {
	render() {
		if (!this.value) return c;
		let e = r.generateAbsolute({ unique: this.value.unique });
		return s`<a href=${e}
      ><archived-user-name-cell
        .name=${this.value.name}
        .stale=${this.value.stale}
        .staleLabel=${this.localize.term("archivedUsers_staleTag")}></archived-user-name-cell
    ></a>`;
	}
	static {
		this.styles = a`
    a {
      color: var(--uui-color-interactive, #1b264f);
      text-decoration: none;
    }
    a:hover,
    a:focus-visible {
      color: var(--uui-color-interactive-emphasis, #3544b1);
      text-decoration: underline;
    }
  `;
	}
};
t([l({ attribute: !1 })], m.prototype, "value", void 0), m = t([o("archived-users-name-column-layout")], m);
// src/archived-view/archived-user-table.element.ts
var h = {
	name: "Name",
	archived: "ArchivedUtc"
}, g = class extends d {
	#e;
	#t;
	constructor() {
		super(), this._items = [], this._selection = [], this._loaded = !1, this.#t = f((e) => this.#o({ filter: e }), 300), this.consumeContext(n, (e) => {
			this.#e = e, this.observe(e?.items, (e) => {
				this._items = e ?? [], this._loaded = !0;
			}, "_items"), this.observe(e?.selection.selection, (e) => this._selection = e ?? [], "_selection");
		});
	}
	#n() {
		return [
			{
				name: this.localize.term("archivedUsers_columnName"),
				alias: "name",
				elementName: "archived-users-name-column-layout",
				allowSorting: !0
			},
			{
				name: this.localize.term("archivedUsers_columnEmail"),
				alias: "email"
			},
			{
				name: this.localize.term("archivedUsers_columnArchived"),
				alias: "archived",
				allowSorting: !0
			},
			{
				name: this.localize.term("archivedUsers_columnArchivedBy"),
				alias: "archivedBy"
			},
			{
				name: this.localize.term("archivedUsers_columnReason"),
				alias: "reason"
			},
			{
				name: this.localize.term("archivedUsers_columnLastLogin"),
				alias: "lastLogin"
			}
		];
	}
	#r() {
		let t = {
			system: this.localize.term("archivedUsers_system"),
			removedUser: this.localize.term("archivedUsers_removedUser")
		};
		return this._items.map((n) => ({
			id: n.unique,
			icon: "icon-user",
			entityType: "user",
			data: [
				{
					columnAlias: "name",
					value: n
				},
				{
					columnAlias: "email",
					value: n.email
				},
				{
					columnAlias: "archived",
					value: this.#i(n.archivedUtc)
				},
				{
					columnAlias: "archivedBy",
					value: e(n, t)
				},
				{
					columnAlias: "reason",
					value: n.reason ?? ""
				},
				{
					columnAlias: "lastLogin",
					value: n.lastLoginDate ? this.#i(n.lastLoginDate) : ""
				}
			]
		}));
	}
	#i(e) {
		return this.localize.date(e, {
			dateStyle: "medium",
			timeStyle: "short"
		});
	}
	#a(e) {
		let t = e.target, n = h[t.orderingColumn];
		n && this.#o({
			orderBy: n,
			orderDirection: t.orderingDesc ? "Descending" : "Ascending"
		});
	}
	#o(e) {
		this.#e?.updateFilter({
			...e,
			skip: 0
		}), this.#e?.loadCollection();
	}
	#s(e) {
		e.stopPropagation(), this.#e?.selection.setSelection(e.target.selection);
	}
	render() {
		return s`
      <uui-input
        id="search"
        label=${this.localize.term("archivedUsers_searchLabel")}
        placeholder=${this.localize.term("archivedUsers_searchLabel")}
        @input=${(e) => this.#t((e.target.value ?? "").trim())}>
        <uui-icon name="icon-search" slot="prepend"></uui-icon>
      </uui-input>
      ${this._loaded && this._items.length === 0 ? s`<p id="empty">${this.localize.term("archivedUsers_noArchivedUsers")}</p>` : s`<umb-table
            .config=${{
			allowSelection: !0,
			hideIcon: !1
		}}
            .columns=${this.#n()}
            .items=${this.#r()}
            .selection=${this._selection}
            @selected=${this.#s}
            @deselected=${this.#s}
            @ordered=${this.#a}></umb-table>`}
    `;
	}
	static {
		this.styles = [a`
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
    `];
	}
};
t([u()], g.prototype, "_items", void 0), t([u()], g.prototype, "_selection", void 0), t([u()], g.prototype, "_loaded", void 0), g = t([o("archived-users-archived-user-table")], g);
export { g as ArchivedUsersArchivedUserTableElement, g as element };
