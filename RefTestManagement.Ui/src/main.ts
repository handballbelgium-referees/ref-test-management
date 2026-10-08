import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';

// This is the one console call left in production, deliberately. If bootstrap rejects there is no
// injector to report through, and nothing has loaded yet, so the error cannot carry participant
// data — it is a startup fault in our own code. Everything after bootstrap goes via ErrorReporter.
bootstrapApplication(App, appConfig).catch((err) => console.error('[bootstrap]', err));
