import { a as r } from "./archived-users.api-2oDNAobH.js";
import { A as s } from "./archive-status.context-token-DXqk9-Pa.js";
import { A as e } from "./index-BkUw13Lw.js";
import { n as o } from "./notify-Cb7LfZzn.js";
import { r as n } from "./refresh-user-DZSrA9g5.js";
import { UmbEntityActionBase as c } from "@umbraco-cms/backoffice/entity-action";
import { umbOpenModal as u } from "@umbraco-cms/backoffice/modal";
class U extends c {
  async execute() {
    const t = this.args.unique;
    if (!t) throw new Error("No user to unarchive.");
    const i = await (await this.getContext(s))?.config();
    if (!await u(this, e, {
      data: { mode: "unarchive", count: 1, requireReason: i?.requireReason ?? !1 }
    }).catch(() => {
    })) return;
    const { data: a } = await r.unarchive(this, [t]);
    a && (await o(this, a, "unarchive"), await n(this, [t], !1));
  }
}
export {
  U as ArchivedUsersUnarchiveEntityAction,
  U as api
};
