import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';

// Fix viewport for high-resolution Android devices
if (typeof window !== 'undefined' && window.innerWidth > 1024 && 'ontouchstart' in window) {
  const viewport = document.querySelector('meta[name="viewport"]');
  if (viewport) {
    const dpr = window.devicePixelRatio || 1;
    if (dpr > 1.5) {
      viewport.setAttribute(
        'content',
        'width=device-width, initial-scale=1, viewport-fit=cover, user-scalable=no'
      );
      // Force layout recalculation
      document.documentElement.style.width = '100%';
    }
  }
}

// This is the one console call left in production, deliberately. If bootstrap rejects there is no
// injector to report through, and nothing has loaded yet, so the error cannot carry participant
// data — it is a startup fault in our own code. Everything after bootstrap goes via ErrorReporter.
bootstrapApplication(App, appConfig).catch((err) => console.error('[bootstrap]', err));
