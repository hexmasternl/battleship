import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { axe } from 'vitest-axe';
import { HelpOverview } from './help-overview';
import { HELP_TOPICS } from './help-topics';

describe('HelpOverview', () => {
  let fixture: ComponentFixture<HelpOverview>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HelpOverview],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(HelpOverview);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  it('has exactly one level-one heading', () => {
    expect(host.querySelectorAll('h1').length).toBe(1);
    expect(host.querySelector('h1')?.textContent?.trim()).toBe('Help');
  });

  it('links back to the landing page', () => {
    expect(host.querySelector('a[href="/"]')).not.toBeNull();
  });

  it('links the six topics in registry order', () => {
    const links = Array.from(host.querySelectorAll('.help-overview__topic'));
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/help/prerequisites',
      '/help/resources',
      '/help/resource-relations',
      '/help/dapr',
      '/help/custom-integration',
      '/help/resource-commands',
    ]);
  });

  it('shows the title, summary and icon of every topic', () => {
    const links = Array.from(host.querySelectorAll('.help-overview__topic'));
    links.forEach((link, i) => {
      const topic = HELP_TOPICS[i];
      expect(link.querySelector('.help-overview__name')?.textContent?.trim()).toBe(topic.title);
      expect(link.querySelector('.help-overview__summary')?.textContent?.trim()).toBe(
        topic.summary,
      );
      expect(link.querySelector('mat-icon')?.textContent?.trim()).toBe(topic.icon);
      expect(link.textContent?.replace(/\s+/g, ' ')).toContain(`${topic.title} ${topic.summary}`);
    });
  });

  it('has no accessibility violations', async () => {
    const results = await axe(host);
    const summary = results.violations.map(
      (v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`,
    );
    expect(summary).toEqual([]);
  }, 20000);
});
