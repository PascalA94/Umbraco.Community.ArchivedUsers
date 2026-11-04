import { t as e } from "./archived-users.api-OwsGzfbt.js";
import { t } from "./archive-status.context-token-BNsNAZBd.js";
import { t as n } from "./archive-confirm.modal-token-vGuUZPCT.js";
import { t as r } from "./notify-Cupf-rCN.js";
import { t as i } from "./reload-collection-DQgNViGD.js";
import { umbOpenModal as a } from "@umbraco-cms/backoffice/modal";
import { UmbEntityBulkActionBase as o } from "@umbraco-cms/backoffice/entity-bulk-action";
// src/bulk-actions/unarchive.bulk-action.ts
var s = class extends o {
	async execute() {
		if (!this.selection.length) return;
		let o = await this.getContext(t), s = await o?.config();
		if (!await a(this, n, { data: {
			mode: "unarchive",
			count: this.selection.length,
			requireReason: s?.requireReason ?? !1
		} }).catch(() => void 0)) return;
		let { data: c } = await e.unarchive(this, [...this.selection]);
		c && (await r(this, c, "unarchive"), await o?.refresh(), await i(this));
	}
};
export { s as ArchivedUsersUnarchiveBulkAction, s as api };
