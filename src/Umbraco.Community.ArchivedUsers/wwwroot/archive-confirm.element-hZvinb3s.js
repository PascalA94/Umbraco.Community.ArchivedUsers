import { nothing as n, html as i, repeat as b, css as z, state as g, customElement as y } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement as A } from "@umbraco-cms/backoffice/modal";
var C = Object.defineProperty, w = Object.getOwnPropertyDescriptor, p = (e) => {
  throw TypeError(e);
}, v = (e, t, a, l) => {
  for (var o = l > 1 ? void 0 : l ? w(t, a) : t, h = e.length - 1, u; h >= 0; h--)
    (u = e[h]) && (o = (l ? u(t, a, o) : u(o)) || o);
  return l && o && C(t, a, o), o;
}, x = (e, t, a) => t.has(e) || p("Cannot " + a), E = (e, t, a) => t.has(e) ? p("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, a), s = (e, t, a) => (x(e, t, "access private method"), a), r, d, m, _, f, $, U;
const M = 500;
let c = class extends A {
  constructor() {
    super(...arguments), E(this, r), this._reason = "";
  }
  render() {
    if (!this.data) return n;
    const e = this.data, t = e.mode === "cleanUp" && e.count === 0;
    return i`
      <uui-dialog-layout headline=${s(this, r, $).call(this)}>
        ${e.mode === "archive" ? i`<p>${this.localize.term("archivedUsers_archiveIntro")}</p>` : n}
        ${e.mode === "unarchive" ? i`<p>${this.localize.term("archivedUsers_unarchiveIntro")}</p>` : n}
        ${e.mode === "cleanUp" ? i`<p id="count">${t ? this.localize.term("archivedUsers_cleanUpNone") : this.localize.term("archivedUsers_cleanUpCount", e.count)}</p>` : n}
        ${e.sampleNames?.length ? i`<p>${this.localize.term("archivedUsers_cleanUpSample")}</p>
              <ul id="sample">
                ${b(e.sampleNames, (a) => a, (a) => i`<li>${a}</li>`)}
              </ul>` : n}
        ${e.showNeverLoggedInHint ? i`<p id="hint">${this.localize.term("archivedUsers_neverLoggedInHint")}</p>` : n}
        ${s(this, r, m).call(this) && !t ? s(this, r, U).call(this, e.requireReason) : n}
        ${t ? i`<uui-button slot="actions" label=${this.localize.term("archivedUsers_close")} @click=${this._rejectModal}></uui-button>` : i`<uui-button slot="actions" label=${this.localize.term("archivedUsers_cancel")} @click=${this._rejectModal}></uui-button>
              <uui-button
                slot="actions"
                id="confirm"
                look="primary"
                color="danger"
                label=${this.localize.term("archivedUsers_confirm")}
                ?disabled=${!s(this, r, d).call(this)}
                @click=${s(this, r, f)}></uui-button>`}
      </uui-dialog-layout>
    `;
  }
};
r = /* @__PURE__ */ new WeakSet();
d = function() {
  return !this.data || this.data.mode === "cleanUp" && this.data.count === 0 ? !1 : !(s(this, r, m).call(this) && this.data.requireReason && !this._reason.trim());
};
m = function() {
  return this.data?.mode === "archive" || this.data?.mode === "cleanUp";
};
_ = function(e) {
  this._reason = e.target.value ?? "";
};
f = function() {
  if (!s(this, r, d).call(this)) return;
  const e = this._reason.trim();
  this.updateValue({ reason: e.length ? e : void 0 }), this._submitModal();
};
$ = function() {
  const e = this.data;
  switch (e.mode) {
    case "archive":
      return e.count === 1 ? this.localize.term("archivedUsers_archiveHeadline") : this.localize.term("archivedUsers_archiveBulkHeadline", e.count);
    case "unarchive":
      return this.localize.term("archivedUsers_unarchiveHeadline", e.count);
    default:
      return this.localize.term("archivedUsers_cleanUpHeadline");
  }
};
U = function(e) {
  return i`
      <uui-form-layout-item>
        <uui-label slot="label" for="reason" ?required=${e}>${this.localize.term("archivedUsers_reason")}</uui-label>
        <span slot="description" id="reason-help">${this.localize.term("archivedUsers_reasonHelp")}</span>
        <uui-textarea
          id="reason"
          label=${this.localize.term("archivedUsers_reason")}
          aria-describedby="reason-help"
          maxlength=${M}
          ?required=${e}
          .value=${this._reason}
          @input=${s(this, r, _)}></uui-textarea>
      </uui-form-layout-item>
    `;
};
c.styles = [
  z`
      uui-dialog-layout {
        max-width: 36rem;
      }
      uui-textarea {
        width: 100%;
      }
      #sample {
        margin-top: 0;
      }
      /* The form item's default description colour fails WCAG AA contrast (a11y.spec.ts). */
      #reason-help {
        color: var(--uui-color-text);
      }
    `
];
v([
  g()
], c.prototype, "_reason", 2);
c = v([
  y("archived-users-archive-confirm-modal")
], c);
export {
  c as ArchivedUsersArchiveConfirmModalElement,
  c as element
};
