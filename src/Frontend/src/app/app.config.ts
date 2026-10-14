import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { apiGatewayInterceptor } from './core/api-gateway.interceptor';
import { BackendConfig, provideBackendConfig } from './core/backend-config';
import { authInterceptor } from './core/session/auth.interceptor';
import { EVENT_SOURCE_FACTORY, EVENTS_API_URL } from './events/events.tokens';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Reads the gateway URL from the runtime config.json before the first backend request.
    provideBackendConfig(),
    // Requests written as /api/<module>/... carry the player's access token (renewed when needed)
    // and go to the gateway. The order matters: the auth interceptor matches on the /api prefix
    // that the gateway interceptor rewrites. See core/session/auth.interceptor.ts and
    // core/api-gateway.interceptor.ts.
    provideHttpClient(withInterceptors([authInterceptor, apiGatewayInterceptor])),
    // Route parameters (such as the game page's `gameCode`) bind straight to component inputs.
    provideRouter(routes, withComponentInputBinding()),
    // The Events API URL from the runtime config.json (`eventsBaseUrl`); null when it is missing,
    // in which case the events service opens no stream. Read after the app initializer has run.
    { provide: EVENTS_API_URL, useFactory: () => inject(BackendConfig).eventsBaseUrl() },
    { provide: EVENT_SOURCE_FACTORY, useValue: (url: string) => new EventSource(url) },
  ],
};
