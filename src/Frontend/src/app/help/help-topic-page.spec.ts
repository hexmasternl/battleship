import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { HelpTopicPage } from './help-topic-page';

describe('HelpTopicPage', () => {
  let fixture: ComponentFixture<HelpTopicPage>;
  let host: HTMLElement;
  let backend: HttpTestingController;

  async function open(slug: string, markdown: string): Promise<void> {
    fixture.componentRef.setInput('slug', slug);
    fixture.detectChanges();
    backend.expectOne(`/docs/${slug}.md`).flush(markdown);
    await fixture.whenStable();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HelpTopicPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HelpTopicPage);
    host = fixture.nativeElement as HTMLElement;
  });

  afterEach(() => backend.verify());

  it('renders the topic document inside the help layout', async () => {
    await open('dapr', '# Dapr\n\nSidecars run next to each app.');
    expect(host.querySelector('battle-help-layout main battle-help-document')).not.toBeNull();
    expect(host.querySelector('.help-prose p')?.textContent).toBe('Sidecars run next to each app.');
  });

  it('has exactly one level-one heading, and it comes from the document', async () => {
    await open('dapr', '# Dapr\n\n## Sidecars\n\nText.');
    const headings = host.querySelectorAll('h1');
    expect(headings.length).toBe(1);
    expect(headings[0].closest('.help-prose')).not.toBeNull();
    expect(headings[0].textContent).toBe('Dapr');
  });

  it('offers the breadcrumb and the neighbouring topics', async () => {
    await open('dapr', '# Dapr');
    const hrefs = Array.from(host.querySelectorAll('nav a')).map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual(['/', '/help', '/help/resource-relations', '/help/custom-integration']);
  });

  it('has no accessibility violations', async () => {
    await open(
      'resources',
      '# Resources\n\nAn Aspire resource.\n\n| Kind | Name |\n|---|---|\n| Container | messaging |\n\n```bash\naspire run\n```',
    );
    const results = await axe(host);
    const summary = results.violations.map(
      (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
    );
    expect(summary).toEqual([]);
  }, 20000);
});
