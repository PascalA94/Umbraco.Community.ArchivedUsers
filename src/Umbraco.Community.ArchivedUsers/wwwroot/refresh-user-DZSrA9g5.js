import { A as m } from "./archive-status.context-token-DXqk9-Pa.js";
import { UmbContextConsumerController as r } from "@umbraco-cms/backoffice/context-api";
import { UMB_USER_DETAIL_STORE_CONTEXT as n } from "@umbraco-cms/backoffice/user";
import { UserStateModel as f } from "@umbraco-cms/backoffice/external/backend-api";
async function _(t, e, s) {
  const a = await new r(t, m).asPromise().catch(() => {
  }), i = await new r(t, n).asPromise().catch(() => {
  });
  for (const o of e)
    await a?.refresh(o), s && i?.updateItem(o, { state: f.DISABLED });
}
export {
  _ as r
};
