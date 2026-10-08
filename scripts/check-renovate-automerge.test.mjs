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
const actionDigestRule = renovate.packageRules.find(
  ({ description }) => description === 'Require human review for GitHub Action digest updates',
);
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

test('GitHub Action digest updates require human review', () => {
  assert.ok(actionDigestRule, 'GitHub Action digest review rule exists');
  assert.deepEqual(actionDigestRule.matchManagers, ['github-actions']);
  assert.deepEqual(actionDigestRule.matchUpdateTypes, ['digest']);
  assert.equal(actionDigestRule.automerge, false);
  assert.equal(actionDigestRule.platformAutomerge, false);

  const digestPinRule = renovate.packageRules.find(
    ({ description }) => description === 'Pin all GitHub Actions to immutable SHA digests for supply-chain security',
  );
  assert.ok(digestPinRule, 'GitHub Actions remain pinned to immutable digests');
  assert.equal(digestPinRule.pinDigests, true);
});

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
