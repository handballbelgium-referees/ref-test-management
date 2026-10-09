---
applyTo: "RefTestManagement.Ui/**"
---

# UI conventions

Angular 22, zoneless and signal-based. Follow the patterns already in the nearest feature; the rules below are the ones that are specific to this repository or easy to get wrong.

- Keep TypeScript strict and avoid `any`. Prefer `signal`, `computed`, `toSignal`, and `inject()`; update signals with `set()` or `update()`, never `mutate()`.
- Use native `@if`, `@for`, and `@switch`; put host bindings and listeners in `host` metadata. Do not use arrow functions in templates or assume browser globals during server-side rendering.
- Do not add redundant `standalone: true` or an explicit `ChangeDetectionStrategy` to new components; preserve existing explicit uses unless the change requires otherwise.
- Follow nearby conventions for `templateUrl` files (paths relative to the component) and Tailwind utility classes. Prefer `class`/`style` bindings over `ngClass`/`ngStyle`.
- Services follow the pattern of the nearest existing service. Avoid unmanaged subscriptions; prefer the async pipe or `rxResource` for template streams.
- Accessibility: semantic controls, accessible names, keyboard interaction, visible focus, and WCAG AA contrast.
- Edit GraphQL documents under `graphql/**/*.graphql`. Never hand-edit `graphql/generated.ts`; run `npm run codegen` with the API running.
- Edit `public/i18n/{en,nl,fr,de}.json` in place. Keep the two-space indentation and the file's existing line endings, and do not re-serialize. `de.json` uses `\u` escapes. Run `npm run check:i18n`; the post-edit hook also warns about mixed line endings and raw non-ASCII in `de.json`.
- Tests use Vitest through Angular's test builder. Run a focused spec from this directory: `npm test -- --include src/path/to/spec.ts --watch=false`.
- Do not expose secrets or private server configuration in UI code, GraphQL documents, or locale files.
