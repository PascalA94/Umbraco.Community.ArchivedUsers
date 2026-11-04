import { UmbContextConsumerController as t } from "@umbraco-cms/backoffice/context-api";
import { UMB_COLLECTION_CONTEXT as l } from "@umbraco-cms/backoffice/collection";
async function i(o) {
  await (await new t(o, l).asPromise().catch(() => {
  }))?.loadCollection();
}
export {
  i as r
};
