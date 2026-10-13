import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Route `data` keys (such as the coming-soon page's `feature`) bind straight to component inputs.
    provideRouter(routes, withComponentInputBinding()),
  ],
};
