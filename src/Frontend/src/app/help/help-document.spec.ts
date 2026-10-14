import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { HelpDocument } from './help-document';

@Component({
  imports: [HelpDocument],
  template: `<main><battle-help-document [file]="file()" /></main>`,
})
class Host {
  readonly file = signal('dapr.md');
}

const SAMPLE = [
  '# Dapr',
  '',
  'A sidecar runs next to each app. See [resources](resources.md#containers).',
  '',
  '## The pub/sub component',
  '',
  '- RabbitMQ',
  '- `integration-events`',
  '',
  '| Resource | Sidecar |',
  '|---|---|',
  '| games-api | yes |',
  '',
  '```bash',
  'dapr init',
  '```',
  '',
  '> The AppHost fails fast.',
  '',
  '![Apps and their sidecars](assets/help-dapr-sidecars.svg)',
  '',
  'Read [the Dapr docs](https://docs.dapr.io/).',
].join('\n');

describe('HelpDocument', () => {
  let fixture: ComponentFixture<Host>;
  let host: HTMLElement;
  let backend: HttpTestingController;

  async function render(markdown: string): Promise<void> {
    fixture.detectChanges();
    backend.expectOne('/docs/dapr.md').flush(markdown);
    await fixture.whenStable();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Host);
    host = fixture.nativeElement as HTMLElement;
  });

  afterEach(() => backend.verify());

  it('shows a loading state while the document is fetched', async () => {
    fixture.detectChanges();
    expect(host.querySelector('[role="status"]')?.textContent).toContain('Loading');
    backend.expectOne('/docs/dapr.md').flush('# Dapr');
    await fixture.whenStable();
    expect(host.querySelector('[role="status"]')).toBeNull();
  });

  it('renders the document as HTML in the prose typography', async () => {
    await render(SAMPLE);
    const prose = host.querySelector('.help-prose');
    expect(prose?.querySelector('h1')?.textContent).toBe('Dapr');
    expect(prose?.querySelectorAll('li').length).toBe(2);
    expect(prose?.querySelector('.help-prose__table table')).not.toBeNull();
    expect(prose?.querySelector('pre code')?.textContent).toContain('dapr init');
    expect(prose?.querySelector('blockquote')).not.toBeNull();
    expect(prose?.querySelector('img')?.getAttribute('src')).toBe(
      '/docs/assets/help-dapr-sidecars.svg',
    );
  });

  it('gives every heading a fragment id', async () => {
    await render(SAMPLE);
    const ids = Array.from(host.querySelectorAll('.help-prose h1, .help-prose h2')).map(
      (h) => h.id,
    );
    expect(ids).toEqual(['dapr', 'the-pubsub-component']);
  });

  it('strips script elements and inline event handlers', async () => {
    await render(
      '# Unsafe\n\n<script>window.hacked = true</script>\n\n<p onclick="alert(1)">Click</p>',
    );
    const prose = host.querySelector('.help-prose');
    expect(prose?.querySelector('script')).toBeNull();
    expect(prose?.innerHTML).not.toContain('window.hacked');
    expect(prose?.querySelector('[onclick]')).toBeNull();
    expect(prose?.textContent).toContain('Click');
  });

  it('keeps target and rel on external links', async () => {
    await render(SAMPLE);
    const external = host.querySelector<HTMLAnchorElement>('a[href="https://docs.dapr.io/"]');
    expect(external?.getAttribute('target')).toBe('_blank');
    expect(external?.getAttribute('rel')).toBe('noopener');
  });

  it('navigates inside the app when a link to another topic is clicked', async () => {
    await render(SAMPLE);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const link = host.querySelector<HTMLAnchorElement>('a.help-link');
    expect(link?.getAttribute('href')).toBe('/help/resources#containers');

    const click = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    link?.dispatchEvent(click);

    expect(click.defaultPrevented).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/help/resources#containers');
  });

  it.each(['ctrlKey', 'metaKey', 'shiftKey', 'altKey'] as const)(
    'leaves a click with %s to the browser',
    async (modifier) => {
      await render(SAMPLE);
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      const click = new MouseEvent('click', {
        bubbles: true,
        cancelable: true,
        button: 0,
        [modifier]: true,
      });
      // Keep jsdom from attempting the navigation itself.
      host.addEventListener('click', (event) => event.preventDefault());
      const notPrevented = host.querySelector('a.help-link')?.dispatchEvent(click);

      expect(navigate).not.toHaveBeenCalled();
      expect(notPrevented).toBe(false);
    },
  );

  it('does not route clicks on external links', async () => {
    await render(SAMPLE);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const click = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    host.addEventListener('click', (event) => event.preventDefault());
    host.querySelector('a[href="https://docs.dapr.io/"]')?.dispatchEvent(click);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('shows an error with a retry action that fetches the document again', async () => {
    fixture.detectChanges();
    backend.expectOne('/docs/dapr.md').flush('gone', { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('could not be loaded');

    host.querySelector<HTMLButtonElement>('button')?.click();
    fixture.detectChanges();
    backend.expectOne('/docs/dapr.md').flush('# Dapr');
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')).toBeNull();
    expect(host.querySelector('.help-prose h1')?.textContent).toBe('Dapr');
  });

  it('has no accessibility violations on a rendered sample', async () => {
    await render(SAMPLE);
    const results = await axe(host);
    const summary = results.violations.map(
      (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
    );
    expect(summary).toEqual([]);
  }, 20000);

  it('has no accessibility violations in the error state', async () => {
    fixture.detectChanges();
    backend.expectOne('/docs/dapr.md').flush('gone', { status: 500, statusText: 'Error' });
    await fixture.whenStable();
    const results = await axe(host);
    expect(results.violations.map((v) => v.id)).toEqual([]);
  }, 20000);
});
