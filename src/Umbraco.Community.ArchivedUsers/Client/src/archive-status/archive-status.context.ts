import { archivedUsersApi, type ArchivedUsersConfigResponseModel } from '../api/archived-users.api.js';
import { ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT } from './archive-status.context-token.js';
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
 */
export class ArchivedUsersArchiveStatusContext extends UmbContextBase {
  #statuses = new UmbObjectState<Record<string, ArchiveStatus | undefined>>({});
  #loaded = false;
  #loading?: Promise<void>;
  #config?: Promise<ArchivedUsersConfigResponseModel | undefined>;

  constructor(host: UmbControllerHost) {
    super(host, ARCHIVED_USERS_ARCHIVE_STATUS_CONTEXT);
  }

  observeIsArchived(unique: string) {
    this.#ensure(unique);
    return this.#statuses.asObservablePart((s) => s[unique]?.isArchived ?? false);
  }

  observeIsStale(unique: string) {
    this.#ensure(unique);
    return this.#statuses.asObservablePart((s) => s[unique]?.isStale ?? false);
  }

  /** Whether the status for this user has been loaded yet. */
  observeIsLoaded(unique: string) {
    this.#ensure(unique);
    return this.#statuses.asObservablePart(() => this.#loaded);
  }

  /**
   * Reloads the archived set. It reads the list route (always 200, small set) rather than GET archived-users/{id},
   * whose 404 for "not archived" would show as a console error on every user editor (Task 13 verification).
   */
  async refresh(_unique?: string): Promise<void> {
    const statuses: Record<string, ArchiveStatus> = {};
    const take = 1000;
    for (let skip = 0; ; skip += take) {
      const { data } = await archivedUsersApi.list(this, { skip, take });
      if (!data) return; // leave the previous state; conditions then keep their last answer
      for (const item of data.items) statuses[item.user.id] = { isArchived: true, isStale: item.stale };
      if (skip + take >= data.total) break;
    }

    this.#loaded = true;
    this.#statuses.setValue(statuses);
  }

  /** Package settings, fetched once per session. */
  config(): Promise<ArchivedUsersConfigResponseModel | undefined> {
    this.#config ??= archivedUsersApi.config(this).then(({ data }) => data);
    return this.#config;
  }

  #ensure(_unique: string) {
    if (this.#loaded || this.#loading) return;
    this.#loading = this.refresh().finally(() => (this.#loading = undefined));
  }
}

export { ArchivedUsersArchiveStatusContext as api };
