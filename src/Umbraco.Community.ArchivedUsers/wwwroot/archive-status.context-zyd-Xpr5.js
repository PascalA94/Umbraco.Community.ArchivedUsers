import { a } from "./archived-users.api-C7fXqz2n.js";
import { A as o } from "./archive-status.context-token-DXqk9-Pa.js";
import { UmbContextBase as h } from "@umbraco-cms/backoffice/class-api";
import { UmbObjectState as n } from "@umbraco-cms/backoffice/observable-api";
class v extends h {
  #t = new n({});
  #s = !1;
  #e;
  #i;
  constructor(t) {
    super(t, o);
  }
  observeIsArchived(t) {
    return this.#r(t), this.#t.asObservablePart((s) => s[t]?.isArchived ?? !1);
  }
  observeIsStale(t) {
    return this.#r(t), this.#t.asObservablePart((s) => s[t]?.isStale ?? !1);
  }
  /** Whether the status for this user has been loaded yet. */
  observeIsLoaded(t) {
    return this.#r(t), this.#t.asObservablePart(() => this.#s);
  }
  /**
   * Reloads the archived set. It reads the list route (always 200, small set) rather than GET archived-users/{id},
   * whose 404 for "not archived" would show as a console error on every user editor (Task 13 verification).
   */
  async refresh(t) {
    const s = {};
    for (let e = 0; ; e += 1e3) {
      const { data: r } = await a.list(this, { skip: e, take: 1e3 });
      if (!r) return;
      for (const i of r.items) s[i.user.id] = { isArchived: !0, isStale: i.stale };
      if (e + 1e3 >= r.total) break;
    }
    this.#s = !0, this.#t.setValue(s);
  }
  /** Package settings, fetched once per session. */
  config() {
    return this.#i ??= a.config(this).then(({ data: t }) => t), this.#i;
  }
  #r(t) {
    this.#s || this.#e || (this.#e = this.refresh().finally(() => this.#e = void 0));
  }
}
export {
  v as ArchivedUsersArchiveStatusContext,
  v as api
};
