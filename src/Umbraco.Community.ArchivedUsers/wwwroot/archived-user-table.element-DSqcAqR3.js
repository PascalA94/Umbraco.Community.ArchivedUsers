import { a as B } from "./map-item-DBjLDJ_H.js";
import { LitElement as C, nothing as L, html as c, css as w, property as D, customElement as T, state as $ } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as z } from "@umbraco-cms/backoffice/lit-element";
import { UMB_EDIT_USER_WORKSPACE_PATH_PATTERN as M } from "@umbraco-cms/backoffice/user";
import { UMB_COLLECTION_CONTEXT as R } from "@umbraco-cms/backoffice/collection";
function W(e, t) {
  let s;
  return (...i) => {
    s !== void 0 && clearTimeout(s), s = setTimeout(() => {
      s = void 0, e(...i);
    }, t);
  };
}
const d = class d extends C {
  constructor() {
    super(), this.name = "", this.stale = !1, this.staleLabel = "";
  }
  render() {
    return c`<span class="name">${this.name}</span>${this.stale ? c` <uui-tag color="warning" look="secondary">${this.staleLabel}</uui-tag>` : L}`;
  }
};
d.properties = {
  name: { type: String },
  stale: { type: Boolean },
  staleLabel: { type: String, attribute: "stale-label" }
}, d.styles = w`
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--uui-size-space-2, 6px);
      flex-wrap: wrap;
    }
  `;
let _ = d;
customElements.get("archived-user-name-cell") || customElements.define("archived-user-name-cell", _);
var N = Object.defineProperty, q = Object.getOwnPropertyDescriptor, E = (e, t, s, i) => {
  for (var a = i > 1 ? void 0 : i ? q(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (a = (i ? o(t, s, a) : o(a)) || a);
  return i && a && N(t, s, a), a;
};
let f = class extends z {
  render() {
    if (!this.value) return L;
    const e = M.generateAbsolute({ unique: this.value.unique });
    return c`<a href=${e}
      ><archived-user-name-cell
        .name=${this.value.name}
        .stale=${this.value.stale}
        .staleLabel=${this.localize.term("archivedUsers_staleTag")}></archived-user-name-cell
    ></a>`;
  }
};
E([
  D({ attribute: !1 })
], f.prototype, "value", 2);
f = E([
  T("archived-users-name-column-layout")
], f);
var I = Object.defineProperty, F = Object.getOwnPropertyDescriptor, S = (e) => {
  throw TypeError(e);
}, v = (e, t, s, i) => {
  for (var a = i > 1 ? void 0 : i ? F(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (a = (i ? o(t, s, a) : o(a)) || a);
  return i && a && I(t, s, a), a;
}, b = (e, t, s) => t.has(e) || S("Cannot " + s), m = (e, t, s) => (b(e, t, "read from private field"), s ? s.call(e) : t.get(e)), p = (e, t, s) => t.has(e) ? S("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, s), G = (e, t, s, i) => (b(e, t, "write to private field"), t.set(e, s), s), l = (e, t, s) => (b(e, t, "access private method"), s), h, y, r, O, P, g, x, A, U;
const H = { name: "Name", archived: "ArchivedUtc" };
let u = class extends z {
  constructor() {
    super(), p(this, r), this._items = [], this._selection = [], this._loaded = !1, p(this, h), p(this, y, W((e) => l(this, r, A).call(this, { filter: e }), 300)), this.consumeContext(R, (e) => {
      G(this, h, e), this.observe(e?.items, (t) => {
        this._items = t ?? [], this._loaded = !0;
      }, "_items"), this.observe(e?.selection.selection, (t) => this._selection = t ?? [], "_selection");
    });
  }
  render() {
    return c`
      <uui-input
        id="search"
        label=${this.localize.term("archivedUsers_searchLabel")}
        placeholder=${this.localize.term("archivedUsers_searchLabel")}
        @input=${(e) => m(this, y).call(this, (e.target.value ?? "").trim())}>
        <uui-icon name="icon-search" slot="prepend"></uui-icon>
      </uui-input>
      ${this._loaded && this._items.length === 0 ? c`<p id="empty">${this.localize.term("archivedUsers_noArchivedUsers")}</p>` : c`<umb-table
            .config=${{ allowSelection: !0, hideIcon: !1 }}
            .columns=${l(this, r, O).call(this)}
            .items=${l(this, r, P).call(this)}
            .selection=${this._selection}
            @selected=${l(this, r, U)}
            @deselected=${l(this, r, U)}
            @ordered=${l(this, r, x)}></umb-table>`}
    `;
  }
};
h = /* @__PURE__ */ new WeakMap();
y = /* @__PURE__ */ new WeakMap();
r = /* @__PURE__ */ new WeakSet();
O = function() {
  return [
    { name: this.localize.term("archivedUsers_columnName"), alias: "name", elementName: "archived-users-name-column-layout", allowSorting: !0 },
    { name: this.localize.term("archivedUsers_columnEmail"), alias: "email" },
    { name: this.localize.term("archivedUsers_columnArchived"), alias: "archived", allowSorting: !0 },
    { name: this.localize.term("archivedUsers_columnArchivedBy"), alias: "archivedBy" },
    { name: this.localize.term("archivedUsers_columnReason"), alias: "reason" },
    { name: this.localize.term("archivedUsers_columnLastLogin"), alias: "lastLogin" }
  ];
};
P = function() {
  const e = {
    system: this.localize.term("archivedUsers_system"),
    removedUser: this.localize.term("archivedUsers_removedUser")
  };
  return this._items.map((t) => ({
    id: t.unique,
    icon: "icon-user",
    entityType: "user",
    data: [
      { columnAlias: "name", value: t },
      { columnAlias: "email", value: t.email },
      { columnAlias: "archived", value: l(this, r, g).call(this, t.archivedUtc) },
      {
        columnAlias: "archivedBy",
        value: B(t, e)
      },
      { columnAlias: "reason", value: t.reason ?? "" },
      { columnAlias: "lastLogin", value: t.lastLoginDate ? l(this, r, g).call(this, t.lastLoginDate) : "" }
    ]
  }));
};
g = function(e) {
  return this.localize.date(e, { dateStyle: "medium", timeStyle: "short" });
};
x = function(e) {
  const t = e.target, s = H[t.orderingColumn];
  s && l(this, r, A).call(this, { orderBy: s, orderDirection: t.orderingDesc ? "Descending" : "Ascending" });
};
A = function(e) {
  m(this, h)?.updateFilter({ ...e, skip: 0 }), m(this, h)?.loadCollection();
};
U = function(e) {
  e.stopPropagation(), m(this, h)?.selection.setSelection(e.target.selection);
};
u.styles = [
  w`
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
    `
];
v([
  $()
], u.prototype, "_items", 2);
v([
  $()
], u.prototype, "_selection", 2);
v([
  $()
], u.prototype, "_loaded", 2);
u = v([
  T("archived-users-archived-user-table")
], u);
export {
  u as ArchivedUsersArchivedUserTableElement,
  u as element
};
