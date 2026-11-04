import { a as i } from "./archived-users.api-2oDNAobH.js";
import { A as n } from "./archive-status.context-token-DXqk9-Pa.js";
import { A as c } from "./index-BkUw13Lw.js";
import { n as m } from "./notify-Cb7LfZzn.js";
import { r as l } from "./reload-collection-CezsmquS.js";
import { UmbCollectionActionBase as p } from "@umbraco-cms/backoffice/collection";
import { UmbContextConsumerController as f } from "@umbraco-cms/backoffice/context-api";
import { umbOpenModal as u } from "@umbraco-cms/backoffice/modal";
class E extends p {
  async execute() {
    const t = await new f(this, n).asPromise().catch(() => {
    }), r = await t?.config(), { data: a } = await i.previewCleanUp(this);
    if (!a) return;
    const e = await u(this, c, {
      data: {
        mode: "cleanUp",
        count: a.count,
        sampleNames: a.sample.map((s) => s.name),
        requireReason: r?.requireReason ?? !1
      }
    }).catch(() => {
    });
    if (!e || a.count === 0) return;
    const { data: o } = await i.cleanUp(this, e.reason);
    o && (await m(this, o, "archive"), await t?.refresh(), await l(this));
  }
}
export {
  E as ArchivedUsersArchiveDisabledCollectionAction,
  E as api
};
