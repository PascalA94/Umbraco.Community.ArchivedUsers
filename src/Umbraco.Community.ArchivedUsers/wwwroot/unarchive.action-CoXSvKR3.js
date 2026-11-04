import { t as e } from "./archived-users.api-B7NN_OKh.js";
import { t } from "./archive-status.context-token-BNsNAZBd.js";
import { t as n } from "./archive-confirm.modal-token-vGuUZPCT.js";
import { t as r } from "./notify-Cupf-rCN.js";
import { t as i } from "./refresh-user-CWL1xfYn.js";
import { umbOpenModal as a } from "@umbraco-cms/backoffice/modal";
import { UmbEntityActionBase as o } from "@umbraco-cms/backoffice/entity-action";
// src/entity-actions/unarchive.action.ts
var s = class extends o {
	async execute() {
		let o = this.args.unique;
		if (!o) throw Error("No user to unarchive.");
		let s = await (await this.getContext(t))?.config();
		if (!await a(this, n, { data: {
			mode: "unarchive",
			count: 1,
			requireReason: s?.requireReason ?? !1
		} }).catch(() => void 0)) return;
		let { data: c } = await e.unarchive(this, [o]);
		c && (await r(this, c, "unarchive"), await i(this, [o], !1));
	}
};
export { s as ArchivedUsersUnarchiveEntityAction, s as api };
