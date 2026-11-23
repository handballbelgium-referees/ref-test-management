import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import releaseConfig from '../.releaserc.mjs';

const renovate = JSON.parse(readFileSync(new URL('../renovate.json', import.meta.url), 'utf8'));
const packageJson = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8'));
const badgeScript = readFileSync(new URL('./generate-badges.mjs', import.meta.url), 'utf8');
const releasePlugins = releaseConfig.plugins.map((plugin) => (Array.isArray(plugin) ? plugin[0] : plugin));
const badgeDependencies = [...badgeScript.matchAll(/from\s+['"]([^'"]+)['"]/g)]
  .map(([, packageName]) => packageName)
  .filter((packageName) => Object.hasOwn(packageJson.devDependencies, packageName));
const releasePackages = new Set(['semantic-release', ...releasePlugins, ...badgeDependencies]);
const autoMergeRule = renovate.packageRules.find(
  ({ description }) => description === 'Auto-merge patch-level npm dev dependencies once CI passes',
);

function exclusionMatchers() {
  return autoMergeRule.matchPackageNames.map((pattern) => {
    const match = /^!\/(.*)\/$/.exec(pattern);
    assert.ok(match, `Expected a negative, exact package-name regex; got ${pattern}`);
    const matcher = new RegExp(match[1]);
    assert.ok(matcher.source.startsWith('^') && matcher.source.endsWith('$'), `Expected exact match; got ${pattern}`);
    return matcher;
  });
}

test('release and badge tooling cannot match npm dev-dependency auto-merge', () => {
  assert.ok(autoMergeRule, 'Patch-level npm dev-dependency auto-merge rule exists');
  assert.ok(releaseConfig.plugins.length > 0, 'Release plugins are configured');
  assert.ok(Object.hasOwn(packageJson.devDependencies, 'semantic-release'));

  const matchers = exclusionMatchers();
  // WP-97 keeps the release executor excluded if the badge hook changes later.
  const expectedExclusions = new Set([...releasePackages, '@semantic-release/exec']);
  const expectedMatcherSources = [...expectedExclusions]
    .map((packageName) => new RegExp(`^${packageName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`).source)
    .sort();
  assert.deepEqual(
    matchers.map((matcher) => matcher.source).sort(),
    expectedMatcherSources,
    'Only exact release and badge tooling package names are excluded',
  );
});

test('unrelated patch auto-merge behavior and manual lockfile maintenance remain unchanged', () => {
  assert.deepEqual(autoMergeRule.matchManagers, ['npm']);
  assert.deepEqual(autoMergeRule.matchDepTypes, ['devDependencies']);
  assert.deepEqual(autoMergeRule.matchUpdateTypes, ['patch']);
  assert.equal(autoMergeRule.automerge, true);
  assert.equal(autoMergeRule.automergeType, 'pr');
  assert.equal(autoMergeRule.platformAutomerge, true);
  assert.equal(renovate.lockFileMaintenance.automerge, false);

  const matchers = exclusionMatchers();
  for (const packageName of ['@commitlint/cli', '@commitlint/config-conventional', 'husky']) {
    assert.ok(Object.hasOwn(packageJson.devDependencies, packageName));
    assert.ok(!matchers.some((matcher) => matcher.test(packageName)), `${packageName} remains eligible`);
  }
});

test('vulnerability alerts are configured only at the top level', () => {
  assert.equal(renovate.vulnerabilityAlerts.enabled, true);
  assert.deepEqual(renovate.vulnerabilityAlerts.schedule, ['at any time']);
  assert.equal(renovate.vulnerabilityAlerts.dependencyDashboardApproval, false);
  assert.equal(renovate.vulnerabilityAlerts.automerge, false);
  assert.ok(
    renovate.packageRules.every((rule) => !Object.hasOwn(rule, 'vulnerabilityAlerts')),
    'Renovate only permits vulnerabilityAlerts at the top level',
  );
});

test('.NET platform major updates share a group and require manual approval', () => {
  const genericMajor = renovate.packageRules.findIndex(
    (rule) => rule.groupName === null && rule.matchUpdateTypes?.includes('major'),
  );
  const dotnetMajor = renovate.packageRules.findIndex(
    (rule) => rule.groupSlug === 'dotnet-platform-major',
  );
  assert.ok(genericMajor >= 0 && dotnetMajor > genericMajor);

  const rule = renovate.packageRules[dotnetMajor];
  assert.equal(rule.groupName, '.NET platform major upgrades');
  assert.deepEqual(rule.matchManagers, ['nuget']);
  assert.deepEqual(rule.matchUpdateTypes, ['major']);
  assert.equal(rule.dependencyDashboardApproval, true);
  assert.equal(rule.automerge, false);

  const matches = (name) => rule.matchPackageNames.some((pattern) =>
    pattern.startsWith('/')
      ? new RegExp(pattern.slice(1, -1), 'i').test(name)
      : pattern.toLowerCase() === name.toLowerCase(),
  );
  for (const name of [
    'dotnet-sdk', 'mcr.microsoft.com/dotnet/aspnet', 'Microsoft.AspNetCore.WebUtilities',
    'Microsoft.EntityFrameworkCore', 'Microsoft.EntityFrameworkCore.SqlServer',
    'Npgsql.EntityFrameworkCore.PostgreSQL', 'MySql.EntityFrameworkCore',
  ]) {
    assert.ok(matches(name), `${name} must share the group`);
  }
  for (const name of ['Microsoft.IdentityModel.Tokens', 'System.Text.Json', 'postgres']) {
    assert.ok(!matches(name), `${name} must stay outside the group`);
  }
});
