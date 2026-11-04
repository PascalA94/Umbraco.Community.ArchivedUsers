import { t as e } from "./archive-status.context-token-BNsNAZBd.js";
import { UMB_USER_WORKSPACE_CONTEXT as t } from "@umbraco-cms/backoffice/user";
import { UmbConditionBase as n } from "@umbraco-cms/backoffice/extension-registry";
// src/conditions/is-archived.condition.ts
var r = class extends n {
	constructor(n, r) {
		super(n, r), this.permitted = !1, this.consumeContext(t, (t) => {
			this.consumeContext(e, (e) => {
				!t || !e ? this.permitted = !1 : this.observe(t.unique, (t) => {
					t ? this.observe(e.observeIsArchived(t), (e) => this.permitted = e, "_archivedUsersStatus") : this.permitted = !1;
				}, "_archivedUsersUser");
			});
		});
	}
};
export { r as ArchivedUsersIsArchivedCondition, r as api };
