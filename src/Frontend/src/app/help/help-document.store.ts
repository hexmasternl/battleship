import { httpResource } from '@angular/common/http';
import { Injectable, Signal, computed, signal } from '@angular/core';
import { HELP_DOCS_PATH, renderHelpMarkdown } from './help-markdown';

/** Where a help document is: being fetched, rendered, or failed to load. */
export type HelpDocumentStatus = 'loading' | 'loaded' | 'error';

/**
 * One help document: fetched as text from the frontend's own `/docs/` path (never the backend
 * gateway) and rendered to HTML. The document follows the file signal handed to `connect`, and
 * `retry` fetches it again after a failure. Provided by `HelpDocument`, so it lives and dies with
 * the component.
 */
@Injectable()
export class HelpDocumentStore {
  /** The file name signal the document follows; `null` until a component connects one. */
  readonly #file = signal<Signal<string> | null>(null);

  readonly #document = httpResource.text(() => {
    const file = this.#file()?.();
    return file === undefined ? undefined : `${HELP_DOCS_PATH}${file}`;
  });

  readonly status = computed<HelpDocumentStatus>(() => {
    switch (this.#document.status()) {
      case 'error':
        return 'error';
      case 'resolved':
      case 'local':
        return 'loaded';
      default:
        return 'loading';
    }
  });

  /** The rendered, not yet sanitised HTML of the loaded document; empty until it has loaded. */
  readonly html = computed(() =>
    this.status() === 'loaded' && this.#document.hasValue()
      ? renderHelpMarkdown(this.#document.value())
      : '',
  );

  /** Follow the document named by `file`, a file in `docs/` such as `dapr.md`. */
  connect(file: Signal<string>): void {
    this.#file.set(file);
  }

  /** Fetch the document again, after it failed to load. */
  retry(): void {
    this.#document.reload();
  }
}
