import { t as e } from "./archived-users.api-B7NN_OKh.js";
import { n as t } from "./map-item-BAWaCWpf.js";
import { UmbRepositoryBase as n } from "@umbraco-cms/backoffice/repository";
// src/archived-view/archived-user-collection.repository.ts
var r = class extends n {
	async requestCollection(n = {
		skip: 0,
		take: 50
	}) {
		let { data: r, error: i } = await e.list(this, {
			skip: n.skip ?? 0,
			take: n.take ?? 50,
			filter: n.filter || void 0,
			orderBy: n.orderBy ?? "ArchivedUtc",
			orderDirection: n.orderDirection ?? "Descending"
		});
		return i || !r ? { error: i } : { data: {
			items: r.items.map(t),
			total: r.total
		} };
	}
};
export { r as ArchivedUserCollectionRepository, r as api };
