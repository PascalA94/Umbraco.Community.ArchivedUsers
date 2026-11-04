import { a as i } from "./archived-users.api-C7fXqz2n.js";
import { UmbRepositoryBase as s } from "@umbraco-cms/backoffice/repository";
function o(e) {
  return {
    unique: e.user.id,
    entityType: "user",
    name: e.user.name,
    email: e.user.email,
    archivedUtc: e.archivedUtc,
    archivedByName: e.archivedBy?.name ?? null,
    archivedBySystem: e.archivedBy == null,
    reason: e.reason ?? null,
    lastLoginDate: e.user.lastLoginDate ?? null,
    stale: e.stale
  };
}
class l extends s {
  async requestCollection(r = { skip: 0, take: 50 }) {
    const { data: a, error: t } = await i.list(this, {
      skip: r.skip ?? 0,
      take: r.take ?? 50,
      filter: r.filter || void 0,
      // Newest archived first by default (F4).
      orderBy: r.orderBy ?? "ArchivedUtc",
      orderDirection: r.orderDirection ?? "Descending"
    });
    return t || !a ? { error: t } : { data: { items: a.items.map(o), total: a.total } };
  }
}
export {
  l as ArchivedUserCollectionRepository,
  l as api
};
