import { UmbContextConsumerController as c } from "@umbraco-cms/backoffice/context-api";
import { UmbLocalizationController as u } from "@umbraco-cms/backoffice/localization-api";
import { UMB_NOTIFICATION_CONTEXT as p } from "@umbraco-cms/backoffice/notification";
const a = ["Self", "Invited", "AlreadyArchived", "NotArchived", "NotPermitted", "NotFound", "Cancelled", "CoreRefused"];
function l(s) {
  const e = { done: 0, skipped: 0, skipReasons: {} };
  for (const o of s) {
    if (o.status === "Archived" || o.status === "Unarchived") {
      e.done++;
      continue;
    }
    e.skipped++;
    const t = o.status.split(":")[0] ?? "CoreRefused", r = a.includes(t) ? t : "CoreRefused";
    e.skipReasons[r] = (e.skipReasons[r] ?? 0) + 1;
  }
  return e;
}
const f = {
  Self: (s, e) => s("archivedUsers_skipSelf", e),
  Invited: (s, e) => s("archivedUsers_skipInvited", e),
  AlreadyArchived: (s, e) => s("archivedUsers_skipAlreadyArchived", e),
  NotArchived: (s, e) => s("archivedUsers_skipNotArchived", e),
  NotPermitted: (s, e) => s("archivedUsers_skipNotPermitted", e),
  NotFound: (s, e) => s("archivedUsers_skipNotFound", e),
  Cancelled: (s, e) => s("archivedUsers_skipCancelled", e),
  CoreRefused: (s, e) => s("archivedUsers_skipCoreRefused", e)
};
function h(s, e, o) {
  const t = o(e === "archive" ? "archivedUsers_archivedCount" : "archivedUsers_unarchivedCount", s.done), r = [s.skipped > 0 ? `${t}, ${o("archivedUsers_skippedCount", s.skipped)}` : t];
  for (const i of a) {
    const d = s.skipReasons[i];
    d && r.push(f[i](o, d));
  }
  return r;
}
async function C(s, e, o) {
  const t = new u(s), r = l(e), i = h(r, o, t.term.bind(t));
  if (e.length === 1) {
    const n = e[0];
    n.status === "Cancelled" && n.message && i.push(n.message), n.status.startsWith("CoreRefused:") && i.push(t.term("archivedUsers_coreRefused", n.status.substring(12)));
  }
  (await new c(s, p).asPromise().catch(() => {
  }))?.peek(r.skipped === 0 ? "positive" : r.done === 0 ? "warning" : "default", { data: { message: i.join(`
`) } });
}
export {
  C as n
};
