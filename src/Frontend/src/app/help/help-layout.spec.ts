import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { HelpLayout } from './help-layout';

@Component({
  imports: [HelpLayout],
  template: `<battle-help-layout [slug]="slug()"><h1>Page</h1></battle-help-layout>`,
})
class Host {
  readonly slug = signal<string | null>(null);
}

describe('HelpLayout', () => {
  let fixture: ComponentFixture<Host>;
  let host: HTMLElement;

  async function render(slug: string | null): Promise<void> {
    fixture.componentInstance.slug.set(slug);
    await fixture.whenStable();
  }

  function hrefs(selector: string): (string | null)[] {
    return Array.from(host.querySelectorAll(selector)).map((a) => a.getAttribute('href'));
  }

  /** The text of the first match with its whitespace collapsed, as a screen reader reads it. */
  function text(selector: string): string {
    return (host.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(Host);
    host = fixture.nativeElement as HTMLElement;
  });

  it('frames the projected content in a main landmark', async () => {
    await render('dapr');
    expect(host.querySelector('main .help-layout__content h1')?.textContent).toBe('Page');
  });

  it('links a topic page back to the landing page and the help overview', async () => {
    await render('dapr');
    expect(hrefs('nav[aria-label="Breadcrumb"] a')).toEqual(['/', '/help']);
    expect(host.querySelector('[aria-current="page"]')?.textContent).toBe('Dapr');
  });

  it('links the overview back to the landing page only', async () => {
    await render(null);
    expect(hrefs('nav[aria-label="Breadcrumb"] a')).toEqual(['/']);
    expect(host.querySelector('nav[aria-label="Help topics"]')).toBeNull();
  });

  it('offers only the next topic on the first topic', async () => {
    await render('prerequisites');
    expect(host.querySelector('.help-layout__step--previous')).toBeNull();
    expect(hrefs('.help-layout__step--next')).toEqual(['/help/resources']);
  });

  it('offers the previous and next topic on a middle topic', async () => {
    await render('dapr');
    expect(hrefs('.help-layout__step--previous')).toEqual(['/help/resource-relations']);
    expect(hrefs('.help-layout__step--next')).toEqual(['/help/custom-integration']);
    expect(text('.help-layout__step--next')).toContain('Next Custom integration');
    expect(text('.help-layout__step--previous')).toContain('Previous Resource relations');
  });

  it('offers only the previous topic on the last topic', async () => {
    await render('resource-commands');
    expect(hrefs('.help-layout__step--previous')).toEqual(['/help/custom-integration']);
    expect(host.querySelector('.help-layout__step--next')).toBeNull();
  });

  it.each([null, 'dapr'])(
    'has no accessibility violations (slug %s)',
    async (slug) => {
      await render(slug);
      const results = await axe(host);
      const summary = results.violations.map(
        (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
      );
      expect(summary).toEqual([]);
    },
    20000,
  );
});
