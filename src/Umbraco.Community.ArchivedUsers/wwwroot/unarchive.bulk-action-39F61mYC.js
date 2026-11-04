import { a } from "./archived-users.api-C7fXqz2n.js";
import { A as s } from "./archive-status.context-token-DXqk9-Pa.js";
import { A as o } from "./index-DvPQOIs4.js";
import { n as r } from "./notify-DthNN_4Z.js";
import { r as n } from "./reload-collection-CezsmquS.js";
import { UmbEntityBulkActionBase as c } from "@umbraco-cms/backoffice/entity-bulk-action";
import { umbOpenModal as h } from "@umbraco-cms/backoffice/modal";
class C extends c {
  async execute() {
    if (!this.selection.length) return;
    const t = await this.getContext(s), e = await t?.config();
    if (!await h(this, o, {
      data: { mode: "unarchive", count: this.selection.length, requireReason: e?.requireReason ?? !1 }
    }).catch(() => {
    })) return;
    const { data: i } = await a.unarchive(this, [...this.selection]);
    i && (await r(this, i, "unarchive"), await t?.refresh(), await n(this));
  }
}
export {
  C as ArchivedUsersUnarchiveBulkAction,
  C as api
};
