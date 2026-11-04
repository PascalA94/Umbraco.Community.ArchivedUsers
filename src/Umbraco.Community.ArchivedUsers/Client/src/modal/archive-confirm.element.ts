import type { ArchiveConfirmModalData, ArchiveConfirmModalValue } from './archive-confirm.modal-token.js';
import { css, customElement, html, nothing, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';

const MAX_REASON = 500;

/**
 * Confirm dialog for archive, unarchive and "Archive disabled users". Every piece of user data (sample names)
 * is a Lit text binding; nothing is passed as HTML (AU-04, design 4 S3).
 */
@customElement('archived-users-archive-confirm-modal')
export class ArchivedUsersArchiveConfirmModalElement extends UmbModalBaseElement<ArchiveConfirmModalData, ArchiveConfirmModalValue> {
  @state()
  private _reason = '';

  #canConfirm(): boolean {
    if (!this.data) return false;
    if (this.data.mode === 'cleanUp' && this.data.count === 0) return false;
    return !(this.#takesReason() && this.data.requireReason && !this._reason.trim());
  }

  #takesReason(): boolean {
    return this.data?.mode === 'archive' || this.data?.mode === 'cleanUp';
  }

  #onInput(event: Event) {
    this._reason = (event.target as HTMLTextAreaElement).value ?? '';
  }

  #confirm() {
    if (!this.#canConfirm()) return;
    const reason = this._reason.trim();
    this.updateValue({ reason: reason.length ? reason : undefined });
    this._submitModal();
  }

  #headline(): string {
    const d = this.data!;
    switch (d.mode) {
      case 'archive':
        return d.count === 1 ? this.localize.term('archivedUsers_archiveHeadline') : this.localize.term('archivedUsers_archiveBulkHeadline', d.count);
      case 'unarchive':
        return this.localize.term('archivedUsers_unarchiveHeadline', d.count);
      default:
        return this.localize.term('archivedUsers_cleanUpHeadline');
    }
  }

  override render() {
    if (!this.data) return nothing;
    const d = this.data;
    const isEmptyCleanUp = d.mode === 'cleanUp' && d.count === 0;

    return html`
      <uui-dialog-layout headline=${this.#headline()}>
        ${d.mode === 'archive' ? html`<p>${this.localize.term('archivedUsers_archiveIntro')}</p>` : nothing}
        ${d.mode === 'unarchive' ? html`<p>${this.localize.term('archivedUsers_unarchiveIntro')}</p>` : nothing}
        ${d.mode === 'cleanUp'
          ? html`<p id="count">${isEmptyCleanUp ? this.localize.term('archivedUsers_cleanUpNone') : this.localize.term('archivedUsers_cleanUpCount', d.count)}</p>`
          : nothing}
        ${d.sampleNames?.length
          ? html`<p>${this.localize.term('archivedUsers_cleanUpSample')}</p>
              <ul id="sample">
                ${repeat(d.sampleNames, (n) => n, (n) => html`<li>${n}</li>`)}
              </ul>`
          : nothing}
        ${d.showNeverLoggedInHint ? html`<p id="hint">${this.localize.term('archivedUsers_neverLoggedInHint')}</p>` : nothing}
        ${this.#takesReason() && !isEmptyCleanUp ? this.#renderReason(d.requireReason) : nothing}
        ${isEmptyCleanUp
          ? html`<uui-button slot="actions" label=${this.localize.term('archivedUsers_close')} @click=${this._rejectModal}></uui-button>`
          : html`<uui-button slot="actions" label=${this.localize.term('archivedUsers_cancel')} @click=${this._rejectModal}></uui-button>
              <uui-button
                slot="actions"
                id="confirm"
                look="primary"
                color="danger"
                label=${this.localize.term('archivedUsers_confirm')}
                ?disabled=${!this.#canConfirm()}
                @click=${this.#confirm}></uui-button>`}
      </uui-dialog-layout>
    `;
  }

  #renderReason(required: boolean) {
    return html`
      <uui-form-layout-item>
        <uui-label slot="label" for="reason" ?required=${required}>${this.localize.term('archivedUsers_reason')}</uui-label>
        <span slot="description" id="reason-help">${this.localize.term('archivedUsers_reasonHelp')}</span>
        <uui-textarea
          id="reason"
          label=${this.localize.term('archivedUsers_reason')}
          aria-describedby="reason-help"
          maxlength=${MAX_REASON}
          ?required=${required}
          .value=${this._reason}
          @input=${this.#onInput}></uui-textarea>
      </uui-form-layout-item>
    `;
  }

  static override styles = [
    css`
      uui-dialog-layout {
        max-width: 36rem;
      }
      uui-textarea {
        width: 100%;
      }
      #sample {
        margin-top: 0;
      }
      /* The form item's default description colour fails WCAG AA contrast (a11y.spec.ts). */
      #reason-help {
        color: var(--uui-color-text);
      }
    `,
  ];
}

export { ArchivedUsersArchiveConfirmModalElement as element };

declare global {
  interface HTMLElementTagNameMap {
    'archived-users-archive-confirm-modal': ArchivedUsersArchiveConfirmModalElement;
  }
}
