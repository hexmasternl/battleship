import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { apiGatewayInterceptor } from './api-gateway.interceptor';
import { BackendConfig } from './backend-config';

const GATEWAY = 'http://localhost:58947';

function setup(baseUrl: string | null): { http: HttpClient; backend: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([apiGatewayInterceptor])),
      provideHttpClientTesting(),
      { provide: BackendConfig, useValue: { baseUrl: signal(baseUrl).asReadonly() } },
    ],
  });
  return { http: TestBed.inject(HttpClient), backend: TestBed.inject(HttpTestingController) };
}

describe('apiGatewayInterceptor', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('rewrites /api/ requests to the gateway and keeps the query string', () => {
    const { http, backend } = setup(GATEWAY);

    http.get('/api/players/me?x=1').subscribe();

    backend.expectOne(`${GATEWAY}/players/me?x=1`).flush({});
  });

  it('keeps query parameters passed as HttpParams', () => {
    const { http, backend } = setup(GATEWAY);

    http.get('/api/games', { params: { page: 2 } }).subscribe();

    const req = backend.expectOne((r) => r.url === `${GATEWAY}/games`);
    expect(req.request.urlWithParams).toBe(`${GATEWAY}/games?page=2`);
    req.flush([]);
  });

  it('leaves a relative non-api URL untouched', () => {
    const { http, backend } = setup(GATEWAY);

    http.get('/config.json').subscribe();
    http.get('/apis/thing').subscribe();

    backend.expectOne('/config.json').flush({});
    backend.expectOne('/apis/thing').flush({});
  });

  it('leaves an absolute URL untouched', () => {
    const { http, backend } = setup(GATEWAY);

    http.get('https://example.com/api/players').subscribe();

    backend.expectOne('https://example.com/api/players').flush({});
  });

  it('fails /api/ requests without sending them when no gateway is configured', async () => {
    const { http, backend } = setup(null);

    const result = firstValueFrom(http.get('/api/players/me'));

    await expect(result).rejects.toBeInstanceOf(HttpErrorResponse);
    await expect(result).rejects.toMatchObject({
      status: 0,
      error: expect.objectContaining({ message: expect.stringContaining('backendBaseUrl') }),
    });
    backend.expectNone(() => true);
  });

  it('still passes non-api requests through when no gateway is configured', () => {
    const { http, backend } = setup(null);

    http.get('/config.json').subscribe();

    backend.expectOne('/config.json').flush({});
  });
});
