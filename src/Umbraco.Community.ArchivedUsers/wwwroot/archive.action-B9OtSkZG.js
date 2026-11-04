import { t as e } from "./archived-users.api-OwsGzfbt.js";
import { t } from "./archive-status.context-token-BNsNAZBd.js";
import { n } from "./predicates-ButY-AVv.js";
import { t as r } from "./archive-confirm.modal-token-vGuUZPCT.js";
import { t as i } from "./notify-Cupf-rCN.js";
import { t as a } from "./refresh-user-CWL1xfYn.js";
import { umbOpenModal as o } from "@umbraco-cms/backoffice/modal";
import { UMB_USER_WORKSPACE_CONTEXT as s } from "@umbraco-cms/backoffice/user";
import { UmbEntityActionBase as c } from "@umbraco-cms/backoffice/entity-action";
// src/entity-actions/archive.action.ts
var l = class extends c {
	async execute() {
		let c = this.args.unique;
		if (!c) throw Error("No user to archive.");
		let l = await (await this.getContext(t))?.config(), u = (await this.getContext(s).catch(() => void 0))?.getData(), d = await o(this, r, { data: {
			mode: "archive",
			count: 1,
			requireReason: l?.requireReason ?? !1,
			showNeverLoggedInHint: u ? n({
				state: u.state,
				lastLoginDate: u.lastLoginDate,
				isArchived: !1
			}) : !1
		} }).catch(() => void 0);
		if (!d) return;
		let { data: f } = await e.archive(this, [c], d.reason);
		f && (await i(this, f, "archive"), await a(this, [c], f[0]?.status === "Archived"));
	}
};
export { l as ArchivedUsersArchiveEntityAction, l as api };
