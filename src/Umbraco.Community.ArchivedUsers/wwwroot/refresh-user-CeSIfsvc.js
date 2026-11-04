import { UmbContextConsumerController as d } from "@umbraco-cms/backoffice/context-api";
import { UmbLocalizationController as u } from "@umbraco-cms/backoffice/localization-api";
import { UMB_NOTIFICATION_CONTEXT as f } from "@umbraco-cms/backoffice/notification";
import { A as p } from "./archive-status.context-token-DXqk9-Pa.js";
import { UMB_USER_DETAIL_STORE_CONTEXT as m } from "@umbraco-cms/backoffice/user";
import { UserStateModel as h } from "@umbraco-cms/backoffice/external/backend-api";
const c = ["Self", "Invited", "AlreadyArchived", "NotArchived", "NotPermitted", "NotFound", "Cancelled", "CoreRefused"];
function l(e) {
  const s = { done: 0, skipped: 0, skipReasons: {} };
  for (const t of e) {
    if (t.status === "Archived" || t.status === "Unarchived") {
      s.done++;
      continue;
    }
    s.skipped++;
    const o = t.status.split(":")[0] ?? "CoreRefused", r = c.includes(o) ? o : "CoreRefused";
    s.skipReasons[r] = (s.skipReasons[r] ?? 0) + 1;
  }
  return s;
}
const v = {
  Self: (e, s) => e("archivedUsers_skipSelf", s),
  Invited: (e, s) => e("archivedUsers_skipInvited", s),
  AlreadyArchived: (e, s) => e("archivedUsers_skipAlreadyArchived", s),
  NotArchived: (e, s) => e("archivedUsers_skipNotArchived", s),
  NotPermitted: (e, s) => e("archivedUsers_skipNotPermitted", s),
  NotFound: (e, s) => e("archivedUsers_skipNotFound", s),
  Cancelled: (e, s) => e("archivedUsers_skipCancelled", s),
  CoreRefused: (e, s) => e("archivedUsers_skipCoreRefused", s)
};
function C(e, s, t) {
  const o = t(s === "archive" ? "archivedUsers_archivedCount" : "archivedUsers_unarchivedCount", e.done), r = [e.skipped > 0 ? `${o}, ${t("archivedUsers_skippedCount", e.skipped)}` : o];
  for (const i of c) {
    const a = e.skipReasons[i];
    a && r.push(v[i](t, a));
  }
  return r;
}
async function S(e, s, t) {
  const o = new u(e), r = l(s), i = C(r, t, o.term.bind(o));
  if (s.length === 1) {
    const n = s[0];
    n.status === "Cancelled" && n.message && i.push(n.message), n.status.startsWith("CoreRefused:") && i.push(o.term("archivedUsers_coreRefused", n.status.substring(12)));
  }
  (await new d(e, f).asPromise().catch(() => {
  }))?.peek(r.skipped === 0 ? "positive" : r.done === 0 ? "warning" : "default", { data: { message: i.join(`
`) } });
}
async function I(e, s, t) {
  const o = await new d(e, p).asPromise().catch(() => {
  }), r = await new d(e, m).asPromise().catch(() => {
  });
  for (const i of s)
    await o?.refresh(i), t && r?.updateItem(i, { state: h.DISABLED });
}
export {
  S as n,
  I as r
};
