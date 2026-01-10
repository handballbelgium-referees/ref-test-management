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

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
