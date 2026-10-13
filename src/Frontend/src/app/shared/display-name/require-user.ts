import { DestroyRef, Directive, ElementRef, Renderer2, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Router, RouterLink } from '@angular/router';
import { UserSession } from '../../core/session/user-session';
import { openDisplayNameDialog } from './display-name-dialog';

/**
 * Gates a `routerLink` on the visitor being a known user. A known user's click is left to
 * `RouterLink`, which navigates at once. For anyone else the navigation is held back: a pending
 * restore is awaited, and when the visitor is still unknown the display-name dialog opens; the link
 * then navigates only once the dialog reports the user created. Modified clicks (new tab, new
 * window) are left to the browser. The link keeps its `href`, role and keyboard behaviour.
 */
@Directive({
  selector: 'a[routerLink][battleRequireUser]',
})
export class RequireUser {
  readonly #session = inject(UserSession);
  readonly #dialog = inject(MatDialog);
  readonly #router = inject(Router);
  readonly #link = inject(RouterLink, { self: true });
  readonly #host = inject<ElementRef<HTMLAnchorElement>>(ElementRef).nativeElement;

  /** Whether a held-back click is being resolved; a second click then opens no second dialog. */
  readonly #busy = signal(false);

  constructor() {
    // A capture listener on the link itself runs before RouterLink's own click listener (DOM
    // order at the target: capturing listeners first), which is the only way to stop RouterLink
    // from navigating; a template `(click)` binding would run after it.
    const onClick = (event: MouseEvent) => this.#onClick(event);
    const stop = inject(Renderer2).listen(this.#host, 'click', onClick, { capture: true });
    inject(DestroyRef).onDestroy(stop);
  }

  #onClick(event: MouseEvent): void {
    if (event.button !== 0 || event.ctrlKey || event.shiftKey || event.altKey || event.metaKey) {
      return;
    }
    if (this.#session.isKnown() && !this.#busy()) return;

    event.preventDefault();
    event.stopImmediatePropagation();
    if (!this.#busy()) void this.#resolve();
  }

  async #resolve(): Promise<void> {
    this.#busy.set(true);
    try {
      const allowed =
        (await this.#session.ensureUser()) ||
        (await openDisplayNameDialog(this.#dialog, this.#host));
      const target = this.#link.urlTree;
      if (allowed && target) await this.#router.navigateByUrl(target);
    } finally {
      this.#busy.set(false);
    }
  }
}
