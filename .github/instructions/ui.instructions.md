---
applyTo: "RefTestManagement.Ui/**"
---

# UI conventions

- Keep TypeScript strict, prefer inference when the type is clear, and avoid `any`.
- Follow the Angular 22 zoneless, signal-based patterns already used here. Prefer `signal`, `computed`, and `toSignal` for state, and `inject()` for dependencies.
- Prefer signal inputs/outputs where they fit. Update signals with `set()` or `update()`, not `mutate()`. Keep components focused and templates simple.
- Components are standalone by default. Do not add redundant `standalone: true` to new components; preserve existing explicit uses unless the change requires otherwise. Do not add explicit `ChangeDetectionStrategy` without a demonstrated need.
- Use native `@if`, `@for`, and `@switch` control flow. Put host bindings/listeners in the `host` metadata rather than `@HostBinding` or `@HostListener`.
- Follow nearby conventions for external `templateUrl` files and Tailwind utility classes; keep template/style paths relative to the component file. Prefer `NgOptimizedImage` for static images when appropriate. Use semantic controls, accessible names, keyboard interaction, visible focus, and WCAG AA contrast.
- Prefer `class`/`style` bindings over `ngClass`/`ngStyle`, and reactive forms for complex form behavior. Do not use arrow functions in templates or assume browser globals are available during server-side rendering.
- In services, prefer `@Service()` in Angular 22+ (or equivalent `@Injectable` metadata where needed), prefer `inject()`, and avoid unmanaged subscriptions. Prefer the async pipe or `rxResource` for template streams.
- Edit GraphQL documents under `graphql/**/*.graphql`. Never hand-edit `graphql/generated.ts`; run `npm run codegen` with the API running.
- Edit `public/i18n/{en,nl,fr,de}.json` in place, keep the two-space indentation and existing CRLF line endings, and do not re-serialize them. `de.json` uses `\u` escapes. Run `npm run check:i18n`.
- Tests use Vitest through Angular's test builder. Run a focused spec with `npm test -- --include src/path/to/spec.ts --watch=false` from this directory.
- Do not expose secrets or private server configuration in UI code, GraphQL documents, or locale files.
