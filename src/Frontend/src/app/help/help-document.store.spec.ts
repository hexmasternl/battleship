import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HelpDocumentStore } from './help-document.store';

describe('HelpDocumentStore', () => {
  let store: HelpDocumentStore;
  let backend: HttpTestingController;

  /** Runs the resource's effects, so a request it is about to send is sent. */
  function send(): void {
    TestBed.tick();
  }

  /** Waits until the answered request has been applied to the resource. */
  async function settle(): Promise<void> {
    await TestBed.inject(ApplicationRef).whenStable();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [HelpDocumentStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(HelpDocumentStore);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('is loading and sends nothing until a document is connected', async () => {
    await settle();
    expect(store.status()).toBe('loading');
    expect(store.html()).toBe('');
    backend.expectNone(() => true);
  });

  it('fetches the document as text from /docs and renders it once loaded', async () => {
    store.connect(signal('dapr.md'));
    send();
    const req = backend.expectOne('/docs/dapr.md');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('text');
    expect(store.status()).toBe('loading');
    expect(store.html()).toBe('');

    req.flush('# Dapr\n\nSidecars everywhere.');
    await settle();
    expect(store.status()).toBe('loaded');
    expect(store.html()).toContain('<h1>Dapr</h1>');
    expect(store.html()).toContain('<p>Sidecars everywhere.</p>');
  });

  it('follows the connected file signal to another document', async () => {
    const file = signal('dapr.md');
    store.connect(file);
    send();
    backend.expectOne('/docs/dapr.md').flush('# Dapr');
    await settle();

    file.set('resources.md');
    send();
    expect(store.status()).toBe('loading');
    backend.expectOne('/docs/resources.md').flush('# Resources');
    await settle();
    expect(store.html()).toContain('<h1>Resources</h1>');
  });

  it('reports an error and fetches again on retry', async () => {
    store.connect(signal('dapr.md'));
    send();
    backend.expectOne('/docs/dapr.md').flush('missing', { status: 404, statusText: 'Not Found' });
    await settle();
    expect(store.status()).toBe('error');
    expect(store.html()).toBe('');

    store.retry();
    send();
    expect(store.status()).toBe('loading');
    backend.expectOne('/docs/dapr.md').flush('# Dapr');
    await settle();
    expect(store.status()).toBe('loaded');
    expect(store.html()).toContain('<h1>Dapr</h1>');
  });

  it('never sends a request to the backend gateway', async () => {
    store.connect(signal('resource-commands.md'));
    send();
    backend.expectNone((req) => req.url.includes('/api/'));
    backend.expectOne('/docs/resource-commands.md').flush('# Commands');
  });
});
