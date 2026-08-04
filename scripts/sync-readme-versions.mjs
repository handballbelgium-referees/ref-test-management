#!/usr/bin/env node
// Keeps the README's Tech Stack version tables in sync with the actual package files.
// Run manually with `node scripts/sync-readme-versions.mjs`, or via the pre-commit hook / CI.
import { readFileSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const rootDir = join(dirname(fileURLToPath(import.meta.url)), '..');

function readText(relativePath) {
  return readFileSync(join(rootDir, relativePath), 'utf8');
}

function packageVersion(propsXml, packageId) {
  const match = propsXml.match(
    new RegExp(`<PackageVersion Include="${packageId}" Version="([^"]+)"`),
  );
  if (!match)
    throw new Error(
      `Package "${packageId}" not found in Directory.Packages.props`,
    );
  return match[1];
}

function npmVersion(pkgJson, name) {
  const raw = pkgJson.dependencies?.[name] ?? pkgJson.devDependencies?.[name];
  if (!raw)
    throw new Error(
      `Dependency "${name}" not found in RefTestManagement.Ui/package.json`,
    );
  return raw.replace(/^[~^]/, '');
}

function toTable(rows) {
  const header = '| Technology | Version | Purpose |\n| --- | --- | --- |';
  const body = rows.map(
    ({ label, version, purpose }) =>
      `| **${label}** | ${version} | ${purpose} |`,
  );
  return [header, ...body].join('\n');
}

function replaceBetweenMarkers(content, marker, table) {
  const pattern = new RegExp(
    `(<!-- versions:${marker}:start -->\\r?\\n\\r?\\n)[\\s\\S]*?(\\r?\\n\\r?\\n<!-- versions:${marker}:end -->)`,
  );
  if (!pattern.test(content))
    throw new Error(`Markers for "${marker}" not found in README.md`);
  return content.replace(pattern, `$1${table}$2`);
}

const propsXml = readText('Directory.Packages.props');
const apiCsproj = readText(
  'RefTestManagement.Api/RefTestManagement.Api.csproj',
);
const uiPackageJson = JSON.parse(readText('RefTestManagement.Ui/package.json'));

const dotnetVersion = apiCsproj.match(
  /<TargetFramework>net([\d.]+)<\/TargetFramework>/,
)?.[1];
if (!dotnetVersion)
  throw new Error('Could not determine .NET target framework version');

const backendRows = [
  {
    label: '.NET',
    version: dotnetVersion,
    purpose: 'Runtime and framework for the Web API',
  },
  {
    label: 'Hot Chocolate',
    version: packageVersion(propsXml, 'HotChocolate.AspNetCore'),
    purpose: 'GraphQL server with authorization, data loaders, and filtering',
  },
  {
    label: 'Entity Framework Core',
    version: packageVersion(propsXml, 'Microsoft.EntityFrameworkCore'),
    purpose: 'ORM for data access and migrations',
  },
  {
    label: 'ClosedXML',
    version: packageVersion(propsXml, 'ClosedXML'),
    purpose: 'Excel report generation',
  },
  {
    label: 'QuestPDF',
    version: packageVersion(propsXml, 'QuestPDF'),
    purpose: 'PDF report generation',
  },
  {
    label: 'SQL Server',
    version: '–',
    purpose: 'Primary data store (Azure SQL or local)',
  },
  {
    label: 'Auth0',
    version: '–',
    purpose: 'OAuth2 / OpenID Connect authentication',
  },
  { label: 'Brevo API', version: '–', purpose: 'Transactional email delivery' },
];

const frontendRows = [
  {
    label: 'Angular',
    version: npmVersion(uiPackageJson, '@angular/core'),
    purpose: 'SPA framework with standalone components and signals',
  },
  {
    label: 'TypeScript',
    version: npmVersion(uiPackageJson, 'typescript'),
    purpose: 'Strict type-checking',
  },
  {
    label: 'Apollo Client',
    version: npmVersion(uiPackageJson, '@apollo/client'),
    purpose: 'GraphQL client with normalized caching',
  },
  {
    label: 'apollo-angular',
    version: npmVersion(uiPackageJson, 'apollo-angular'),
    purpose: 'Angular integration for Apollo Client',
  },
  {
    label: 'ngx-translate',
    version: npmVersion(uiPackageJson, '@ngx-translate/core'),
    purpose: 'i18n and localization',
  },
  {
    label: 'Tailwind CSS',
    version: npmVersion(uiPackageJson, 'tailwindcss'),
    purpose: 'Utility-first styling',
  },
  {
    label: 'GraphQL Code Generator',
    version: npmVersion(uiPackageJson, '@graphql-codegen/cli'),
    purpose: 'Generates TypeScript types from the GraphQL schema',
  },
  {
    label: 'Vitest',
    version: npmVersion(uiPackageJson, 'vitest'),
    purpose: 'Unit testing framework',
  },
  {
    label: 'RxJS',
    version: npmVersion(uiPackageJson, 'rxjs'),
    purpose: 'Reactive programming',
  },
];

const readmePath = join(rootDir, 'README.md');
const original = readFileSync(readmePath, 'utf8');

let updated = replaceBetweenMarkers(original, 'backend', toTable(backendRows));
updated = replaceBetweenMarkers(updated, 'frontend', toTable(frontendRows));

if (updated === original) {
  console.log('README.md version tables already up to date.');
} else {
  writeFileSync(readmePath, updated);
  console.log('README.md version tables updated.');
}
