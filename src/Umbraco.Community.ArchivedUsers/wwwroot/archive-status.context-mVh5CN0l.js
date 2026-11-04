import { t as e } from "./archived-users.api-B7NN_OKh.js";
import { t } from "./archive-status.context-token-BNsNAZBd.js";
import { UmbContextBase as n } from "@umbraco-cms/backoffice/class-api";
import { UmbObjectState as r } from "@umbraco-cms/backoffice/observable-api";
// src/archive-status/cache-on-success.ts
function i(e) {
	let t, n;
	return () => t === void 0 ? (n ??= e().then((e) => (t = e ?? void 0, e)).finally(() => n = void 0), n) : Promise.resolve(t);
}
// src/archive-status/freshness.ts
var a = 3e4;
function o(e, t, n = a) {
	return e === void 0 || t - e >= n;
}
// src/archive-status/latest-only.ts
function s() {
	let e = 0;
	return {
		start: () => ++e,
		isLatest: (t) => t === e
	};
}
// src/archive-status/archive-status.context.ts
var c = class extends n {
	#e = new r({
		loaded: !1,
		statuses: {}
	});
	#t;
	#n;
	#r = 0;
	#i;
	#a = s();
	#o = i(() => e.config(this).then(({ data: e }) => e));
	constructor(e) {
		super(e, t);
	}
	observeIsArchived(e) {
		return this.#s(e), this.#e.asObservablePart((t) => t.statuses[e]?.isArchived ?? !1);
	}
	observeIsStale(e) {
		return this.#s(e), this.#e.asObservablePart((t) => t.statuses[e]?.isStale ?? !1);
	}
	observeIsLoaded(e) {
		return this.#s(e), this.#e.asObservablePart((e) => e.loaded);
	}
	async refresh(t) {
		let n = this.#a.start(), r = {}, i = 1e3;
		for (let t = 0;; t += i) {
			let { data: n } = await e.listQuietly(this, {
				skip: t,
				take: i
			});
			if (!n) {
				this.#n === void 0 && this.#r++ < 3 && (clearTimeout(this.#i), this.#i = setTimeout(() => this.#s(""), 2e3));
				return;
			}
			for (let e of n.items) r[e.user.id] = {
				isArchived: !0,
				isStale: e.stale
			};
			if (t + i >= n.total) break;
		}
		this.#a.isLatest(n) && (this.#n = Date.now(), this.#r = 0, this.#e.setValue({
			loaded: !0,
			statuses: r
		}));
	}
	destroy() {
		clearTimeout(this.#i), super.destroy();
	}
	config() {
		return this.#o();
	}
	#s(e) {
		this.#t || !o(this.#n, Date.now()) || (this.#t = this.refresh().finally(() => this.#t = void 0));
	}
};
export { c as ArchivedUsersArchiveStatusContext, c as api };
