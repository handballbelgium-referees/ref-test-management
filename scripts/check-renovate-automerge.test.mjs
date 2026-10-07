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
  // WP-97 intentionally excludes this release plugin even though it is not active in the current configuration.
  const expectedExclusions = new Set([...releasePackages, '@semantic-release/exec']);
  for (const packageName of expectedExclusions) {
    assert.ok(matchers.some((matcher) => matcher.test(packageName)), `${packageName} is excluded`);
  }
  for (const matcher of matchers) {
    assert.ok([...expectedExclusions].some((packageName) => matcher.test(packageName)), 'Exclusions stay release-specific');
  }
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
