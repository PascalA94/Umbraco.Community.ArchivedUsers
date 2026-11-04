import { UMB_COLLECTION_CONTEXT as e } from "@umbraco-cms/backoffice/collection";
import { UmbContextConsumerController as t } from "@umbraco-cms/backoffice/context-api";
// src/bulk-actions/reload-collection.ts
async function n(n) {
	await (await new t(n, e).asPromise().catch(() => void 0))?.loadCollection();
}
export { n as t };
