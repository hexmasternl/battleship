import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { apiGatewayInterceptor } from './core/api-gateway.interceptor';
import { provideBackendConfig } from './core/backend-config';
import { EVENT_SOURCE_FACTORY, EVENTS_API_URL } from './events/events.tokens';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Reads the gateway URL from the runtime config.json before the first backend request.
    provideBackendConfig(),
    // Requests written as /api/<module>/... go to the gateway; see core/api-gateway.interceptor.ts.
    provideHttpClient(withInterceptors([apiGatewayInterceptor])),
    // Route `data` keys (such as the coming-soon page's `feature`) bind straight to component inputs.
    provideRouter(routes, withComponentInputBinding()),
    // The Events API as the Aspire AppHost serves it in development (see README, "Real-time events").
    { provide: EVENTS_API_URL, useValue: 'http://localhost:5317' },
    { provide: EVENT_SOURCE_FACTORY, useValue: (url: string) => new EventSource(url) },
  ],
};
