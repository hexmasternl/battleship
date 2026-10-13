import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { throwError } from 'rxjs';
import { BackendConfig } from './backend-config';

const API_PREFIX = '/api/';

/**
 * Sends every request written as `/api/<module>/...` to the backend gateway: `/api` is replaced by
 * the gateway base URL from the runtime config, the rest of the path and the query string are kept.
 * Without a configured gateway the request fails here and is never sent. Other URLs pass through.
 */
export const apiGatewayInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_PREFIX)) return next(req);

  const baseUrl = inject(BackendConfig).baseUrl();
  if (baseUrl === null) {
    return throwError(
      () =>
        new HttpErrorResponse({
          url: req.url,
          status: 0,
          statusText: 'Backend gateway not configured',
          error: new Error(
            `Cannot send ${req.method} ${req.url}: "backendBaseUrl" is not configured in config.json.`,
          ),
        }),
    );
  }

  return next(req.clone({ url: `${baseUrl}/${req.url.slice(API_PREFIX.length)}` }));
};
