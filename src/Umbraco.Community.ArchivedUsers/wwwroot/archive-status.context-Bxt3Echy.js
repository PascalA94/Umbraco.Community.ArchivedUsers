import { a as n } from "./archived-users.api-C7fXqz2n.js";
import { A as l } from "./archive-status.context-token-DXqk9-Pa.js";
import { UmbContextBase as h } from "@umbraco-cms/backoffice/class-api";
import { UmbObjectState as u } from "@umbraco-cms/backoffice/observable-api";
function d(i) {
  let e, t;
  return () => e !== void 0 ? Promise.resolve(e) : (t ??= i().then((s) => (e = s ?? void 0, s)).finally(() => t = void 0), t);
}
class m extends h {
  // The loaded flag lives in the observed state: with no archived users the map stays {}, so a flag outside the
  // state would never emit and the conditions would wait forever (Task 18 finding on a fresh site).
  #e = new u({ loaded: !1, statuses: {} });
  #t;
  #r = d(() => n.config(this).then(({ data: e }) => e));
  constructor(e) {
    super(e, l);
  }
  observeIsArchived(e) {
    return this.#s(e), this.#e.asObservablePart((t) => t.statuses[e]?.isArchived ?? !1);
  }
  observeIsStale(e) {
    return this.#s(e), this.#e.asObservablePart((t) => t.statuses[e]?.isStale ?? !1);
  }
  /** Whether the status for this user has been loaded yet. */
  observeIsLoaded(e) {
    return this.#s(e), this.#e.asObservablePart((t) => t.loaded);
  }
  /**
   * Reloads the archived set. It reads the list route (always 200, small set) rather than GET archived-users/{id},
   * whose 404 for "not archived" would show as a console error on every user editor (Task 13 verification).
   */
  async refresh(e) {
    const t = {};
    for (let r = 0; ; r += 1e3) {
      const { data: a } = await n.list(this, { skip: r, take: 1e3 });
      if (!a) return;
      for (const o of a.items) t[o.user.id] = { isArchived: !0, isStale: o.stale };
      if (r + 1e3 >= a.total) break;
    }
    this.#e.setValue({ loaded: !0, statuses: t });
  }
  /** Package settings, fetched once per session (a failed fetch is retried on the next call). */
  config() {
    return this.#r();
  }
  #s(e) {
    this.#e.getValue().loaded || this.#t || (this.#t = this.refresh().finally(() => this.#t = void 0));
  }
}
export {
  m as ArchivedUsersArchiveStatusContext,
  m as api
};
