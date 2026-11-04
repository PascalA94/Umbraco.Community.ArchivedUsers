import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextConsumerController } from '@umbraco-cms/backoffice/context-api';
import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';

/** Reloads the collection the action runs in, as core's bulk actions do (docs/spikes.md S6). */
export async function reloadCollection(host: UmbControllerHost): Promise<void> {
  const collection = await new UmbContextConsumerController(host, UMB_COLLECTION_CONTEXT).asPromise().catch(() => undefined);
  await collection?.loadCollection();
}
