import { summariseResults, type ArchiveResult } from './summarise.js';
import { formatSummary } from './format-summary.js';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextConsumerController } from '@umbraco-cms/backoffice/context-api';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';

/**
 * Shows the per-user outcome. Only the plain-text `message` field is used, which core renders as a text binding;
 * core's HTML variant of the field is never used (AU-04, docs/spikes.md S6).
 */
export async function notifyResults(host: UmbControllerHost, results: ArchiveResult[], mode: 'archive' | 'unarchive'): Promise<void> {
  const localize = new UmbLocalizationController(host);
  const summary = summariseResults(results);
  // formatSummary only ever passes literal keys (P11).
  const lines = formatSummary(summary, mode, localize.term.bind(localize));

  // Single-user extras: a handler's cancel message or core's refusal, as text.
  if (results.length === 1) {
    const only = results[0];
    if (only.status === 'Cancelled' && only.message) lines.push(only.message);
    if (only.status.startsWith('CoreRefused:')) lines.push(localize.term('archivedUsers_coreRefused', only.status.substring('CoreRefused:'.length)));
  }

  const context = await new UmbContextConsumerController(host, UMB_NOTIFICATION_CONTEXT).asPromise().catch(() => undefined);
  context?.peek(summary.skipped === 0 ? 'positive' : summary.done === 0 ? 'warning' : 'default', { data: { message: lines.join('\n') } });
}
