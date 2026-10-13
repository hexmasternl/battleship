import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { provideBackendConfig } from './core/backend-config';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Reads the gateway URL from the runtime config.json before the first backend request.
    provideBackendConfig(),
    // Route `data` keys (such as the coming-soon page's `feature`) bind straight to component inputs.
    provideRouter(routes, withComponentInputBinding()),
  ],
};
