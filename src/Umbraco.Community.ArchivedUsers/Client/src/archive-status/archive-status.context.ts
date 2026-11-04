import { archivedUsersApi, type ArchivedUsersConfigResponseModel } from '../api/archived-users.api.js';
import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from './archive-status.context-token.js';
import { cacheOnSuccess } from './cache-on-success.js';
import { needsRefresh } from './freshness.js';
import { latestOnly } from './latest-only.js';
import { UmbContextBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbObjectState } from '@umbraco-cms/backoffice/observable-api';

interface ArchiveStatus {
  isArchived: boolean;
  isStale: boolean;
}

/**
 * Global cache of the archived users the current user may see, keyed by user unique; a user who is absent is
 * not archived (or not visible, which hides the actions anyway). The CanArchive and IsArchived conditions
 * observe it, so they re-evaluate as soon as an action refreshes a user (docs/spikes.md S6).
 * Opening a user editor reloads it once it is 30 seconds old, so another admin's archive or unarchive shows
 * without a full page reload (review C-9).
 */
export class ArchivedUsersArchiveStatusContext extends UmbContextBase {
  // The loaded flag lives in the observed state: with no archived users the map stays {}, so a flag outside the
  // state would never emit and the conditions would wait forever (Task 18 finding on a fresh site).
  #state = new UmbObjectState<{ loaded: boolean; statuses: Record<string, ArchiveStatus | undefined> }>({ loaded: false, statuses: {} });
  #loading?: Promise<void>;
  #loadedAt?: number;
  #failedLoads = 0;
  #retryTimer?: ReturnType<typeof setTimeout>;
  #loads = latestOnly();
  #config = cacheOnSuccess(() => archivedUsersApi.config(this).then(({ data }) => data));

  constructor(host: UmbControllerHost) {
    super(host, ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT);
  }

  observeIsArchived(unique: string) {
    this.#ensure(unique);
    return this.#state.asObservablePart((s) => s.statuses[unique]?.isArchived ?? false);
  }

  observeIsStale(unique: string) {
    this.#ensure(unique);
    return this.#state.asObservablePart((s) => s.statuses[unique]?.isStale ?? false);
  }

  /** Whether the status for this user has been loaded yet. */
  observeIsLoaded(unique: string) {
    this.#ensure(unique);
    return this.#state.asObservablePart((s) => s.loaded);
  }

  /**
   * Reloads the archived set. It reads the list route (always 200, small set) rather than GET archived-users/{id},
   * whose 404 for "not archived" would show as a console error on every user editor (Task 13 verification).
   */
  async refresh(_unique?: string): Promise<void> {
    const load = this.#loads.start();
    const statuses: Record<string, ArchiveStatus> = {};
    const take = 1000;
    for (let skip = 0; ; skip += take) {
      const { data } = await archivedUsersApi.listQuietly(this, { skip, take });
      if (!data) {
        // Leave the previous state; conditions keep their last answer. A failed first load would otherwise leave
        // both actions hidden until another user is opened, so retry it a few times (review C-9).
        if (this.#loadedAt === undefined && this.#failedLoads++ < 3) {
          clearTimeout(this.#retryTimer);
          this.#retryTimer = setTimeout(() => this.#ensure(''), 2_000);
        }
        return;
      }
      for (const item of data.items) statuses[item.user.id] = { isArchived: true, isStale: item.stale };
      if (skip + take >= data.total) break;
    }

    if (!this.#loads.isLatest(load)) return; // a newer load started; let it write (review A2-5)
    this.#loadedAt = Date.now();
    this.#failedLoads = 0;
    this.#state.setValue({ loaded: true, statuses });
  }

  override destroy(): void {
    clearTimeout(this.#retryTimer);
    super.destroy();
  }

  /** Package settings, fetched once per session (a failed fetch is retried on the next call). */
  config(): Promise<ArchivedUsersConfigResponseModel | undefined> {
    return this.#config();
  }

  #ensure(_unique: string) {
    if (this.#loading || !needsRefresh(this.#loadedAt, Date.now())) return;
    this.#loading = this.refresh().finally(() => (this.#loading = undefined));
  }
}

export { ArchivedUsersArchiveStatusContext as api };
