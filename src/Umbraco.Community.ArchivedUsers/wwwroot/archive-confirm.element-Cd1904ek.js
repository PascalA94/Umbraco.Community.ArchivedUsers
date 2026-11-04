import { t as e } from "./decorate-DsqXQ0nF.js";
import { UmbModalBaseElement as t } from "@umbraco-cms/backoffice/modal";
import { css as n, customElement as r, html as i, nothing as a, repeat as o, state as s } from "@umbraco-cms/backoffice/external/lit";
// src/modal/archive-confirm.element.ts
var c = 500, l = class extends t {
	constructor(...e) {
		super(...e), this._reason = "";
	}
	#e() {
		return !this.data || this.data.mode === "cleanUp" && this.data.count === 0 ? !1 : !(this.#t() && this.data.requireReason && !this._reason.trim());
	}
	#t() {
		return this.data?.mode === "archive" || this.data?.mode === "cleanUp";
	}
	#n(e) {
		this._reason = e.target.value ?? "";
	}
	#r() {
		if (!this.#e()) return;
		let e = this._reason.trim();
		this.updateValue({ reason: e.length ? e : void 0 }), this._submitModal();
	}
	#i() {
		let e = this.data;
		switch (e.mode) {
			case "archive": return e.count === 1 ? this.localize.term("archivedUsers_archiveHeadline") : this.localize.term("archivedUsers_archiveBulkHeadline", e.count);
			case "unarchive": return this.localize.term("archivedUsers_unarchiveHeadline", e.count);
			default: return this.localize.term("archivedUsers_cleanUpHeadline");
		}
	}
	render() {
		if (!this.data) return a;
		let e = this.data, t = e.mode === "cleanUp" && e.count === 0;
		return i`
      <uui-dialog-layout headline=${this.#i()}>
        ${e.mode === "archive" ? i`<p>${this.localize.term("archivedUsers_archiveIntro")}</p>` : a}
        ${e.mode === "unarchive" ? i`<p>${this.localize.term("archivedUsers_unarchiveIntro")}</p>` : a}
        ${e.mode === "cleanUp" ? i`<p id="count">${t ? this.localize.term("archivedUsers_cleanUpNone") : this.localize.term("archivedUsers_cleanUpCount", e.count)}</p>` : a}
        ${e.sampleNames?.length ? i`<p>${this.localize.term("archivedUsers_cleanUpSample")}</p>
              <ul id="sample">
                ${o(e.sampleNames, (e) => e, (e) => i`<li>${e}</li>`)}
              </ul>` : a}
        ${e.showNeverLoggedInHint ? i`<p id="hint">${this.localize.term("archivedUsers_neverLoggedInHint")}</p>` : a}
        ${this.#t() && !t ? this.#a(e.requireReason) : a}
        ${t ? i`<uui-button slot="actions" label=${this.localize.term("archivedUsers_close")} @click=${this._rejectModal}></uui-button>` : i`<uui-button slot="actions" label=${this.localize.term("archivedUsers_cancel")} @click=${this._rejectModal}></uui-button>
              <uui-button
                slot="actions"
                id="confirm"
                look="primary"
                color="danger"
                label=${this.localize.term("archivedUsers_confirm")}
                ?disabled=${!this.#e()}
                @click=${this.#r}></uui-button>`}
      </uui-dialog-layout>
    `;
	}
	#a(e) {
		return i`
      <uui-form-layout-item>
        <uui-label slot="label" for="reason" ?required=${e}>${this.localize.term("archivedUsers_reason")}</uui-label>
        <span slot="description" id="reason-help">${this.localize.term("archivedUsers_reasonHelp")}</span>
        <uui-textarea
          id="reason"
          label=${this.localize.term("archivedUsers_reason")}
          aria-describedby="reason-help"
          maxlength=${c}
          ?required=${e}
          .value=${this._reason}
          @input=${this.#n}></uui-textarea>
      </uui-form-layout-item>
    `;
	}
	static {
		this.styles = [n`
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
    `];
	}
};
e([s()], l.prototype, "_reason", void 0), l = e([r("archived-users-archive-confirm-modal")], l);
export { l as ArchivedUsersArchiveConfirmModalElement, l as element };
