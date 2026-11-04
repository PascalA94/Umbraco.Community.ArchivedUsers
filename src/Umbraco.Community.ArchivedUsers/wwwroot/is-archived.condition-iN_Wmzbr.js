import { A as h } from "./archive-status.context-token-DXqk9-Pa.js";
import { UmbConditionBase as m } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_USER_WORKSPACE_CONTEXT as d } from "@umbraco-cms/backoffice/user";
class C extends m {
  constructor(r, i) {
    super(r, i), this.permitted = !1, this.consumeContext(d, (e) => {
      this.consumeContext(h, (s) => {
        if (!e || !s) {
          this.permitted = !1;
          return;
        }
        this.observe(
          e.unique,
          (t) => {
            if (!t) {
              this.permitted = !1;
              return;
            }
            this.observe(s.observeIsArchived(t), (o) => this.permitted = o, "_archivedUsersStatus");
          },
          "_archivedUsersUser"
        );
      });
    });
  }
}
export {
  C as ArchivedUsersIsArchivedCondition,
  C as api
};
