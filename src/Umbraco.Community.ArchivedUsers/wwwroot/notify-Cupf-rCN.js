import { UmbContextConsumerController as e } from "@umbraco-cms/backoffice/context-api";
import { UmbLocalizationController as t } from "@umbraco-cms/backoffice/localization-api";
import { UMB_NOTIFICATION_CONTEXT as n } from "@umbraco-cms/backoffice/notification";
// src/results/summarise.ts
var r = [
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
function i(e) {
	let t = {
		done: 0,
		skipped: 0,
		skipReasons: {}
	};
	for (let n of e) {
		if (n.status === "Archived" || n.status === "Unarchived") {
			t.done++;
			continue;
		}
		t.skipped++;
		let e = n.status.split(":")[0] ?? "CoreRefused", i = r.includes(e) ? e : "CoreRefused";
		t.skipReasons[i] = (t.skipReasons[i] ?? 0) + 1;
	}
	return t;
}
// src/results/format-summary.ts
var a = {
	Self: (e, t) => e("archivedUsers_skipSelf", t),
	Invited: (e, t) => e("archivedUsers_skipInvited", t),
	AlreadyArchived: (e, t) => e("archivedUsers_skipAlreadyArchived", t),
	NotArchived: (e, t) => e("archivedUsers_skipNotArchived", t),
	NotPermitted: (e, t) => e("archivedUsers_skipNotPermitted", t),
	NotFound: (e, t) => e("archivedUsers_skipNotFound", t),
	Cancelled: (e, t) => e("archivedUsers_skipCancelled", t),
	CoreRefused: (e, t) => e("archivedUsers_skipCoreRefused", t),
	DisabledNotArchived: (e, t) => e("archivedUsers_skipDisabledNotArchived", t),
	Failed: (e, t) => e("archivedUsers_skipFailed", t)
};
function o(e, t, n) {
	let i = n(t === "archive" ? "archivedUsers_archivedCount" : "archivedUsers_unarchivedCount", e.done), o = [e.skipped > 0 ? `${i}, ${n("archivedUsers_skippedCount", e.skipped)}` : i];
	for (let t of r) {
		let r = e.skipReasons[t];
		r && o.push(a[t](n, r));
	}
	return o;
}
// src/results/notify.ts
async function s(r, a, s) {
	let c = new t(r), l = i(a), u = o(l, s, c.term.bind(c));
	if (a.length === 1) {
		let e = a[0];
		e.status === "Cancelled" && e.message && u.push(e.message), e.status.startsWith("CoreRefused:") && u.push(c.term("archivedUsers_coreRefused", e.status.substring(12)));
	}
	(await new e(r, n).asPromise().catch(() => void 0))?.peek(l.skipped === 0 ? "positive" : l.done === 0 ? "warning" : "default", { data: { message: u.join("\n") } });
}
export { s as t };
