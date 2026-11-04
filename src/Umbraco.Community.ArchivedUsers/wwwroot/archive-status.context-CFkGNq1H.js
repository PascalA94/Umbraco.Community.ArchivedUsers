import { a as n } from "./archived-users.api-C7fXqz2n.js";
import { A as h } from "./archive-status.context-token-DXqk9-Pa.js";
import { UmbContextBase as d } from "@umbraco-cms/backoffice/class-api";
import { UmbObjectState as l } from "@umbraco-cms/backoffice/observable-api";
function u(s) {
  let e, t;
  return () => e !== void 0 ? Promise.resolve(e) : (t ??= s().then((i) => (e = i ?? void 0, i)).finally(() => t = void 0), t);
}
const f = 3e4;
function c(s, e, t = f) {
  return s === void 0 || e - s >= t;
}
class S extends d {
  // The loaded flag lives in the observed state: with no archived users the map stays {}, so a flag outside the
  // state would never emit and the conditions would wait forever (Task 18 finding on a fresh site).
  #e = new l({ loaded: !1, statuses: {} });
  #s;
  #i;
  #r = 0;
  #a = u(() => n.config(this).then(({ data: e }) => e));
  constructor(e) {
    super(e, h);
  }
  observeIsArchived(e) {
    return this.#t(e), this.#e.asObservablePart((t) => t.statuses[e]?.isArchived ?? !1);
  }
  observeIsStale(e) {
    return this.#t(e), this.#e.asObservablePart((t) => t.statuses[e]?.isStale ?? !1);
  }
  /** Whether the status for this user has been loaded yet. */
  observeIsLoaded(e) {
    return this.#t(e), this.#e.asObservablePart((t) => t.loaded);
  }
  /**
   * Reloads the archived set. It reads the list route (always 200, small set) rather than GET archived-users/{id},
   * whose 404 for "not archived" would show as a console error on every user editor (Task 13 verification).
   */
  async refresh(e) {
    const t = {};
    for (let r = 0; ; r += 1e3) {
      const { data: a } = await n.list(this, { skip: r, take: 1e3 });
      if (!a) {
        this.#i === void 0 && this.#r++ < 3 && setTimeout(() => this.#t(""), 2e3);
        return;
      }
      for (const o of a.items) t[o.user.id] = { isArchived: !0, isStale: o.stale };
      if (r + 1e3 >= a.total) break;
    }
    this.#i = Date.now(), this.#r = 0, this.#e.setValue({ loaded: !0, statuses: t });
  }
  /** Package settings, fetched once per session (a failed fetch is retried on the next call). */
  config() {
    return this.#a();
  }
  #t(e) {
    this.#s || !c(this.#i, Date.now()) || (this.#s = this.refresh().finally(() => this.#s = void 0));
  }
}
export {
  S as ArchivedUsersArchiveStatusContext,
  S as api
};
