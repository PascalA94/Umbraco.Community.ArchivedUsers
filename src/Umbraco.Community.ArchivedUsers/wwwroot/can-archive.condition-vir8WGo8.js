import { t as e } from "./archive-status.context-token-BNsNAZBd.js";
import { t } from "./predicates-ButY-AVv.js";
import { UMB_USER_WORKSPACE_CONTEXT as n } from "@umbraco-cms/backoffice/user";
import { observeMultiple as r } from "@umbraco-cms/backoffice/observable-api";
import { UmbConditionBase as i } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_CURRENT_USER_CONTEXT as a } from "@umbraco-cms/backoffice/current-user";
// src/conditions/condition-inputs.ts
var o = (e, t) => e.toLowerCase() === t.toLowerCase();
function s(e, t, n, r, i) {
	return {
		isSelf: o(e.unique, t.unique),
		state: e.state,
		isArchived: n.isArchived,
		isStale: n.isStale,
		targetIsAdmin: e.userGroupUniques.some((e) => o(e, r)),
		targetIsSuperUser: o(e.unique, i),
		performerIsAdmin: t.isAdmin
	};
}
// src/conditions/can-archive.condition.ts
var c = class extends i {
	constructor(i, o) {
		super(i, o), this.permitted = !1, this.consumeContext(a, (i) => {
			this.consumeContext(n, (n) => {
				this.consumeContext(e, async (e) => {
					!i || !n || !e ? this.permitted = !1 : this.observe(r([
						i.unique,
						i.isAdmin,
						n.unique,
						n.state,
						n.userGroupUniques
					]), async ([n, i, a, o, c]) => {
						let l = await e.config();
						l ? !a || !n ? this.permitted = !1 : this.observe(r([
							e.observeIsLoaded(a),
							e.observeIsArchived(a),
							e.observeIsStale(a)
						]), ([e, r, u]) => {
							this.permitted = e && t(s({
								unique: a,
								state: o,
								userGroupUniques: c.map((e) => e.unique)
							}, {
								unique: n,
								isAdmin: i ?? !1
							}, {
								isArchived: r,
								isStale: u
							}, l.adminGroupId, l.superUserId));
						}, "_archivedUsersStatus") : this.permitted = !1;
					}, "_archivedUsersUser");
				});
			});
		});
	}
};
export { c as ArchivedUsersCanArchiveCondition, c as api };
