import { a as s } from "./archived-users.api-C7fXqz2n.js";
import { A as e } from "./archive-status.context-token-DXqk9-Pa.js";
import { A as r } from "./index-BzyUosXS.js";
import { n as o, r as n } from "./refresh-user-CeSIfsvc.js";
import { UmbEntityActionBase as c } from "@umbraco-cms/backoffice/entity-action";
import { umbOpenModal as u } from "@umbraco-cms/backoffice/modal";
class w extends c {
  async execute() {
    const t = this.args.unique;
    if (!t) throw new Error("No user to unarchive.");
    const i = await (await this.getContext(e))?.config();
    if (!await u(this, r, {
      data: { mode: "unarchive", count: 1, requireReason: i?.requireReason ?? !1 }
    }).catch(() => {
    })) return;
    const { data: a } = await s.unarchive(this, [t]);
    a && (await o(this, a, "unarchive"), await n(this, [t], !1));
  }
}
export {
  w as ArchivedUsersUnarchiveEntityAction,
  w as api
};
