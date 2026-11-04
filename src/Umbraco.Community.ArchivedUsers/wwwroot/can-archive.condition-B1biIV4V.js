import { A as v } from "./archive-status.context-token-DXqk9-Pa.js";
import { c as S } from "./predicates-BAyNkNWg.js";
import { UmbConditionBase as l } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_USER_WORKSPACE_CONTEXT as _ } from "@umbraco-cms/backoffice/user";
import { UMB_CURRENT_USER_CONTEXT as I } from "@umbraco-cms/backoffice/current-user";
import { observeMultiple as p } from "@umbraco-cms/backoffice/observable-api";
const a = (e, s) => e.toLowerCase() === s.toLowerCase();
function E(e, s, n, t, r) {
  return {
    isSelf: a(e.unique, s.unique),
    state: e.state,
    isArchived: n.isArchived,
    isStale: n.isStale,
    targetIsAdmin: e.userGroupUniques.some((i) => a(i, t)),
    targetIsSuperUser: a(e.unique, r),
    performerIsAdmin: s.isAdmin
  };
}
class N extends l {
  constructor(s, n) {
    super(s, n), this.permitted = !1, this.consumeContext(I, (t) => {
      this.consumeContext(_, (r) => {
        this.consumeContext(v, async (i) => {
          if (!t || !r || !i) {
            this.permitted = !1;
            return;
          }
          this.observe(
            p([t.unique, t.isAdmin, r.unique, r.state, r.userGroupUniques]),
            async ([d, u, o, f, h]) => {
              const m = await i.config();
              if (!m) {
                this.permitted = !1;
                return;
              }
              if (!o || !d) {
                this.permitted = !1;
                return;
              }
              this.observe(
                p([i.observeIsLoaded(o), i.observeIsArchived(o), i.observeIsStale(o)]),
                ([c, A, C]) => {
                  this.permitted = c && S(
                    E(
                      { unique: o, state: f, userGroupUniques: h.map((U) => U.unique) },
                      { unique: d, isAdmin: u ?? !1 },
                      { isArchived: A, isStale: C },
                      m.adminGroupId,
                      m.superUserId
                    )
                  );
                },
                "_archivedUsersStatus"
              );
            },
            "_archivedUsersUser"
          );
        });
      });
    });
  }
}
export {
  N as ArchivedUsersCanArchiveCondition,
  N as api
};
