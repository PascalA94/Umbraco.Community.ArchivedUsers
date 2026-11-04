import { t as e } from "./archived-users.api-B7NN_OKh.js";
import { t } from "./archive-status.context-token-BNsNAZBd.js";
import { t as n } from "./archive-confirm.modal-token-vGuUZPCT.js";
import { t as r } from "./notify-Cupf-rCN.js";
import { t as i } from "./reload-collection-DQgNViGD.js";
import { umbOpenModal as a } from "@umbraco-cms/backoffice/modal";
import { UmbCollectionActionBase as o } from "@umbraco-cms/backoffice/collection";
import { UmbContextConsumerController as s } from "@umbraco-cms/backoffice/context-api";
// src/collection-action/archive-disabled.action.ts
var c = class extends o {
	async execute() {
		let o = await new s(this, t).asPromise().catch(() => void 0), c = await o?.config(), { data: l } = await e.previewCleanUp(this);
		if (!l) return;
		let u = await a(this, n, { data: {
			mode: "cleanUp",
			count: l.count,
			sampleNames: l.sample.map((e) => e.name),
			requireReason: c?.requireReason ?? !1
		} }).catch(() => void 0);
		if (!u || l.count === 0) return;
		let { data: d } = await e.cleanUp(this, u.reason);
		d && (await r(this, d, "archive"), await o?.refresh(), await i(this));
	}
};
export { c as ArchivedUsersArchiveDisabledCollectionAction, c as api };
