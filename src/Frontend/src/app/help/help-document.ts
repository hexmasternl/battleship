import { Component, ElementRef, afterRenderEffect, inject, input, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { HelpDocumentStore } from './help-document.store';
import { HELP_LINK_CLASS, helpHeadingIds } from './help-markdown';

/**
 * Renders one help document from `docs/`: a loading state, an error with a retry action, or the
 * document as sanitised HTML in the `.help-prose` typography (src/styles/_help-prose.scss).
 * Links to other help topics navigate inside the app.
 */
@Component({
  selector: 'battle-help-document',
  providers: [HelpDocumentStore],
  host: { '(click)': 'followHelpLink($event)' },
  template: `
    @switch (store.status()) {
      @case ('loading') {
        <p class="help-document__loading" role="status">Loading the help topic…</p>
      }
      @case ('error') {
        <div class="help-document__error">
          <p class="bs-status bs-status--danger" role="alert">
            This help topic could not be loaded.
          </p>
          <button type="button" class="bs-btn bs-btn--ghost" (click)="store.retry()">
            Try again
          </button>
        </div>
      }
      @case ('loaded') {
        <div #prose class="help-prose" [innerHTML]="store.html()"></div>
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .help-document__loading {
      margin: 0;
      color: var(--bs-color-text-dim);
    }

    .help-document__error {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--bs-space-4);
    }
  `,
})
export class HelpDocument {
  /** The document in `docs/`, such as `dapr.md`. */
  readonly file = input.required<string>();

  protected readonly store = inject(HelpDocumentStore);
  readonly #router = inject(Router);
  private readonly prose = viewChild<ElementRef<HTMLElement>>('prose');

  constructor() {
    this.store.connect(this.file);

    // Angular's sanitiser removes `id` attributes, so heading fragment ids are applied to the
    // rendered DOM instead: the DOM is the only place they can live.
    afterRenderEffect({
      write: () => {
        const prose = this.prose()?.nativeElement;
        this.store.html();
        if (prose === undefined) return;
        const headings = Array.from(prose.querySelectorAll<HTMLElement>('h1, h2, h3, h4, h5, h6'));
        const ids = helpHeadingIds(headings.map((heading) => heading.textContent ?? ''));
        headings.forEach((heading, i) => (heading.id = ids[i]));
      },
    });
  }

  /**
   * A plain left click on a link to another help topic navigates inside the app; a click with a
   * modifier key or another button keeps the browser's behaviour (a new tab or window).
   */
  protected followHelpLink(event: MouseEvent): void {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }
    const link = (event.target as Element | null)?.closest?.(`a.${HELP_LINK_CLASS}`);
    const href = link?.getAttribute('href');
    if (!href) return;
    event.preventDefault();
    void this.#router.navigateByUrl(href);
  }
}
