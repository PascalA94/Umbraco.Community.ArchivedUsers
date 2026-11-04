import { a as s } from "./archived-users.api-2oDNAobH.js";
import { A as o } from "./archive-status.context-token-DXqk9-Pa.js";
import { A as r } from "./index-BkUw13Lw.js";
import { n } from "./notify-Cb7LfZzn.js";
import { r as c } from "./reload-collection-CezsmquS.js";
import { UmbEntityBulkActionBase as h } from "@umbraco-cms/backoffice/entity-bulk-action";
import { umbOpenModal as m } from "@umbraco-cms/backoffice/modal";
class C extends h {
  async execute() {
    if (!this.selection.length) return;
    const t = await this.getContext(o), a = await t?.config(), i = await m(this, r, {
      data: { mode: "archive", count: this.selection.length, requireReason: a?.requireReason ?? !1 }
    }).catch(() => {
    });
    if (!i) return;
    const { data: e } = await s.archive(this, [...this.selection], i.reason);
    e && (await n(this, e, "archive"), await t?.refresh(), await c(this));
  }
}
export {
  C as ArchivedUsersArchiveBulkAction,
  C as api
};
