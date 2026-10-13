import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { EVENT_SOURCE_FACTORY, EVENTS_API_URL } from './events/events.tokens';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Route `data` keys (such as the coming-soon page's `feature`) bind straight to component inputs.
    provideRouter(routes, withComponentInputBinding()),
    // The Events API as the Aspire AppHost serves it in development (see README, "Real-time events").
    { provide: EVENTS_API_URL, useValue: 'http://localhost:5317' },
    { provide: EVENT_SOURCE_FACTORY, useValue: (url: string) => new EventSource(url) },
  ],
};
