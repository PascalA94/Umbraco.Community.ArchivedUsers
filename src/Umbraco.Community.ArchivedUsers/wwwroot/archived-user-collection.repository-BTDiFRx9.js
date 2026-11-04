import { a as o } from "./archived-users.api-2oDNAobH.js";
import { m as i } from "./map-item-DBjLDJ_H.js";
import { UmbRepositoryBase as s } from "@umbraco-cms/backoffice/repository";
class d extends s {
  async requestCollection(e = { skip: 0, take: 50 }) {
    const { data: r, error: t } = await o.list(this, {
      skip: e.skip ?? 0,
      take: e.take ?? 50,
      filter: e.filter || void 0,
      // Newest archived first by default (F4).
      orderBy: e.orderBy ?? "ArchivedUtc",
      orderDirection: e.orderDirection ?? "Descending"
    });
    return t || !r ? { error: t } : { data: { items: r.items.map(i), total: r.total } };
  }
}
export {
  d as ArchivedUserCollectionRepository,
  d as api
};
