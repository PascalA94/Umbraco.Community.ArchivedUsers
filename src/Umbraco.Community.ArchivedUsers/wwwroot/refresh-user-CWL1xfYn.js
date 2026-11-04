import { t as e } from "./archive-status.context-token-BNsNAZBd.js";
import { UMB_USER_DETAIL_STORE_CONTEXT as t } from "@umbraco-cms/backoffice/user";
import { UmbContextConsumerController as n } from "@umbraco-cms/backoffice/context-api";
import { UserStateModel as r } from "@umbraco-cms/backoffice/external/backend-api";
// src/entity-actions/refresh-user.ts
async function i(i, a, o) {
	let s = await new n(i, e).asPromise().catch(() => void 0), c = await new n(i, t).asPromise().catch(() => void 0);
	for (let e of a) await s?.refresh(e), o && c?.updateItem(e, { state: r.DISABLED });
}
export { i as t };
