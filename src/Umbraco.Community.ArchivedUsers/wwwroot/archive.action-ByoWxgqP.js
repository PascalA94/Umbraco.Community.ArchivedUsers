import { a as o } from "./archived-users.api-Bpj9imgz.js";
import { A as r } from "./archive-status.context-token-DXqk9-Pa.js";
import { s as n } from "./predicates-BAyNkNWg.js";
import { A as c } from "./index-7z9pf705.js";
import { n as h } from "./notify-Cb7LfZzn.js";
import { r as m } from "./refresh-user-DZSrA9g5.js";
import { UmbEntityActionBase as f } from "@umbraco-cms/backoffice/entity-action";
import { umbOpenModal as u } from "@umbraco-cms/backoffice/modal";
import { UMB_USER_WORKSPACE_CONTEXT as p } from "@umbraco-cms/backoffice/user";
class I extends f {
  async execute() {
    const t = this.args.unique;
    if (!t) throw new Error("No user to archive.");
    const i = await (await this.getContext(r))?.config(), a = (await this.getContext(p).catch(() => {
    }))?.getData(), e = await u(this, c, {
      data: {
        mode: "archive",
        count: 1,
        requireReason: i?.requireReason ?? !1,
        showNeverLoggedInHint: a ? n({ state: a.state, lastLoginDate: a.lastLoginDate }) : !1
      }
    }).catch(() => {
    });
    if (!e) return;
    const { data: s } = await o.archive(this, [t], e.reason);
    s && (await h(this, s, "archive"), await m(this, [t], s[0]?.status === "Archived"));
  }
}
export {
  I as ArchivedUsersArchiveEntityAction,
  I as api
};
