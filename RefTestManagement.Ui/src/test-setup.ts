const { jsdom } = globalThis as typeof globalThis & { jsdom: { window: Window } };

// Vitest exposes the jsdom instance separately from Node's Web Storage global.
Object.defineProperty(globalThis, 'localStorage', {
  configurable: true,
  writable: true,
  value: jsdom.window.localStorage,
});
