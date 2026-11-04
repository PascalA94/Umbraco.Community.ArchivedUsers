import { a as h } from "./archived-users.api-2oDNAobH.js";
import { A as l } from "./archive-status.context-token-DXqk9-Pa.js";
import { UmbContextBase as u } from "@umbraco-cms/backoffice/class-api";
import { UmbObjectState as d } from "@umbraco-cms/backoffice/observable-api";
function c(s) {
  let t, e;
  return () => t !== void 0 ? Promise.resolve(t) : (e ??= s().then((r) => (t = r ?? void 0, r)).finally(() => e = void 0), e);
}
const f = 3e4;
function v(s, t, e = f) {
  return s === void 0 || t - s >= e;
}
function m() {
  let s = 0;
  return {
    start: () => ++s,
    isLatest: (t) => t === s
  };
}
class T extends u {
  // The loaded flag lives in the observed state: with no archived users the map stays {}, so a flag outside the
  // state would never emit and the conditions would wait forever (Task 18 finding on a fresh site).
  #t = new d({ loaded: !1, statuses: {} });
  #s;
  #r;
  #a = 0;
  #i;
  #o = m();
  #n = c(() => h.config(this).then(({ data: t }) => t));
  constructor(t) {
    super(t, l);
  }
  observeIsArchived(t) {
    return this.#e(t), this.#t.asObservablePart((e) => e.statuses[t]?.isArchived ?? !1);
  }
  observeIsStale(t) {
    return this.#e(t), this.#t.asObservablePart((e) => e.statuses[t]?.isStale ?? !1);
  }
  /** Whether the status for this user has been loaded yet. */
  observeIsLoaded(t) {
    return this.#e(t), this.#t.asObservablePart((e) => e.loaded);
  }
  /**
   * Reloads the archived set. It reads the list route (always 200, small set) rather than GET archived-users/{id},
   * whose 404 for "not archived" would show as a console error on every user editor (Task 13 verification).
   */
  async refresh(t) {
    const e = this.#o.start(), r = {}, i = 1e3;
    for (let a = 0; ; a += i) {
      const { data: o } = await h.listQuietly(this, { skip: a, take: i });
      if (!o) {
        this.#r === void 0 && this.#a++ < 3 && (clearTimeout(this.#i), this.#i = setTimeout(() => this.#e(""), 2e3));
        return;
      }
      for (const n of o.items) r[n.user.id] = { isArchived: !0, isStale: n.stale };
      if (a + i >= o.total) break;
    }
    this.#o.isLatest(e) && (this.#r = Date.now(), this.#a = 0, this.#t.setValue({ loaded: !0, statuses: r }));
  }
  destroy() {
    clearTimeout(this.#i), super.destroy();
  }
  /** Package settings, fetched once per session (a failed fetch is retried on the next call). */
  config() {
    return this.#n();
  }
  #e(t) {
    this.#s || !v(this.#r, Date.now()) || (this.#s = this.refresh().finally(() => this.#s = void 0));
  }
}
export {
  T as ArchivedUsersArchiveStatusContext,
  T as api
};
