import { UmbContextConsumerController as c } from "@umbraco-cms/backoffice/context-api";
import { UmbLocalizationController as u } from "@umbraco-cms/backoffice/localization-api";
import { UMB_NOTIFICATION_CONTEXT as l } from "@umbraco-cms/backoffice/notification";
const a = [
  "Self",
  "Invited",
  "AlreadyArchived",
  "NotArchived",
  "NotPermitted",
  "NotFound",
  "Cancelled",
  "CoreRefused",
  "DisabledNotArchived",
  "Failed"
];
function p(s) {
  const e = { done: 0, skipped: 0, skipReasons: {} };
  for (const i of s) {
    if (i.status === "Archived" || i.status === "Unarchived") {
      e.done++;
      continue;
    }
    e.skipped++;
    const o = i.status.split(":")[0] ?? "CoreRefused", r = a.includes(o) ? o : "CoreRefused";
    e.skipReasons[r] = (e.skipReasons[r] ?? 0) + 1;
  }
  return e;
}
const h = {
  Self: (s, e) => s("archivedUsers_skipSelf", e),
  Invited: (s, e) => s("archivedUsers_skipInvited", e),
  AlreadyArchived: (s, e) => s("archivedUsers_skipAlreadyArchived", e),
  NotArchived: (s, e) => s("archivedUsers_skipNotArchived", e),
  NotPermitted: (s, e) => s("archivedUsers_skipNotPermitted", e),
  NotFound: (s, e) => s("archivedUsers_skipNotFound", e),
  Cancelled: (s, e) => s("archivedUsers_skipCancelled", e),
  CoreRefused: (s, e) => s("archivedUsers_skipCoreRefused", e),
  DisabledNotArchived: (s, e) => s("archivedUsers_skipDisabledNotArchived", e),
  Failed: (s, e) => s("archivedUsers_skipFailed", e)
};
function v(s, e, i) {
  const o = i(e === "archive" ? "archivedUsers_archivedCount" : "archivedUsers_unarchivedCount", s.done), r = [s.skipped > 0 ? `${o}, ${i("archivedUsers_skippedCount", s.skipped)}` : o];
  for (const t of a) {
    const d = s.skipReasons[t];
    d && r.push(h[t](i, d));
  }
  return r;
}
async function C(s, e, i) {
  const o = new u(s), r = p(e), t = v(r, i, o.term.bind(o));
  if (e.length === 1) {
    const n = e[0];
    n.status === "Cancelled" && n.message && t.push(n.message), n.status.startsWith("CoreRefused:") && t.push(o.term("archivedUsers_coreRefused", n.status.substring(12)));
  }
  (await new c(s, l).asPromise().catch(() => {
  }))?.peek(r.skipped === 0 ? "positive" : r.done === 0 ? "warning" : "default", { data: { message: t.join(`
`) } });
}
export {
  C as n
};
