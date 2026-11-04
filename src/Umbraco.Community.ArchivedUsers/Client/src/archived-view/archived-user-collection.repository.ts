import { archivedUsersApi, type ArchivedUserOrder, type DirectionModel } from '../api/archived-users.api.js';
import { mapItem, type ArchivedUserCollectionItem } from './map-item.js';
import { UmbRepositoryBase } from '@umbraco-cms/backoffice/repository';
import type { UmbCollectionFilterModel, UmbCollectionRepository } from '@umbraco-cms/backoffice/collection';

export interface ArchivedUserCollectionFilter extends UmbCollectionFilterModel {
  filter?: string;
  orderBy?: ArchivedUserOrder;
  orderDirection?: DirectionModel;
}

/** Reads GET archived-users. The server returns only the archived users the current user may see. */
export class ArchivedUserCollectionRepository extends UmbRepositoryBase implements UmbCollectionRepository<ArchivedUserCollectionItem, ArchivedUserCollectionFilter> {
  async requestCollection(filter: ArchivedUserCollectionFilter = { skip: 0, take: 50 }) {
    const { data, error } = await archivedUsersApi.list(this, {
      skip: filter.skip ?? 0,
      take: filter.take ?? 50,
      filter: filter.filter || undefined,
      // Newest archived first by default (F4).
      orderBy: filter.orderBy ?? 'ArchivedUtc',
      orderDirection: filter.orderDirection ?? 'Descending',
    });
    if (error || !data) return { error };
    return { data: { items: data.items.map(mapItem), total: data.total } };
  }
}

export { ArchivedUserCollectionRepository as api };
