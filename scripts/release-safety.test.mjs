import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import yaml from 'js-yaml';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const SCRIPT = join(ROOT, 'scripts/generate-badges.mjs');
const WORKFLOWS = Object.fromEntries(
  ['beta-release', 'stable-release', 'pr'].map((name) => {
    const path = join(ROOT, `.github/workflows/${name}.yml`);
    const source = readFileSync(path, 'utf8');
    return [name, { source, workflow: yaml.load(source) }];
  }),
);
const PROVENANCE_FIELDS = [
  'node-version',
  'npm-version',
  'dotnet-sdk-version',
  'dotnet-sdks',
  'dotnet-runtimes',
  'runner-os',
  'runner-arch',
  'runner-image-os',
  'runner-image-version',
];

function git(cwd, args) {
  return execFileSync('git', args, { cwd, encoding: 'utf8' }).trim();
}

function remoteGit(remote, args) {
  return execFileSync('git', ['--git-dir', remote, ...args], { encoding: 'utf8' }).trim();
}

function createRepository() {
  const directory = mkdtempSync(join(tmpdir(), 'release-safety-'));
  const remote = join(directory, 'remote.git');
  const repo = join(directory, 'candidate');
  execFileSync('git', ['init', '--bare', '--initial-branch=main', remote], { encoding: 'utf8' });
  execFileSync('git', ['init', '-b', 'main', repo], { encoding: 'utf8' });
  git(repo, ['config', 'core.autocrlf', 'false']);
  git(repo, ['config', 'user.name', 'Release safety test']);
  git(repo, ['config', 'user.email', 'release-safety@example.invalid']);
  mkdirSync(join(repo, 'badges'));
  writeFileSync(join(repo, 'README.md'), 'validated candidate\n');
  writeFileSync(join(repo, 'badges/release.png'), Buffer.from('stable badge'));
  writeFileSync(join(repo, 'badges/pre-release.png'), Buffer.from('old beta badge'));
  git(repo, ['add', '.']);
  git(repo, ['commit', '-m', 'test: validated candidate']);
  const validatedSha = git(repo, ['rev-parse', 'HEAD']);
  git(repo, ['remote', 'add', 'origin', remote]);
  git(repo, ['push', '-u', 'origin', 'main']);
  return { directory, remote, repo, validatedSha };
}

function runBadgePrepare({ repo, validatedSha, outputPath }) {
  return spawnSync(process.execPath, [SCRIPT, '3.2.1-alpha.1', '--publish'], {
    cwd: repo,
    encoding: 'utf8',
    env: {
      ...process.env,
      BADGE_BRANCH: 'main',
      GITHUB_OUTPUT: outputPath,
      VALIDATED_SHA: validatedSha,
    },
  });
}

function remoteBranchSha(remote, branch = 'main') {
  return remoteGit(remote, ['rev-parse', `refs/heads/${branch}`]);
}

test('workflow YAML preserves triggers, read-only fork README sync, and stable deployment ordering', () => {
  const stable = WORKFLOWS['stable-release'].workflow;
  const beta = WORKFLOWS['beta-release'].workflow;
  const pr = WORKFLOWS.pr.workflow;

  assert.ok(Object.hasOwn(stable.on, 'workflow_dispatch'));
  assert.deepEqual(beta.on.push.branches, ['main']);
  assert.deepEqual(pr.on.pull_request.branches, ['main']);
  assert.ok(stable.jobs['deploy-production'].needs.includes('sync-to-main'));
  assert.deepEqual(pr.permissions, { contents: 'read' });
  assert.deepEqual(stable.jobs.release.permissions, { contents: 'read' });
  assert.deepEqual(stable.jobs['deploy-production'].permissions, {
    'id-token': 'write',
    contents: 'read',
    actions: 'read',
  });
  assert.deepEqual(beta.jobs.release.permissions, { contents: 'read' });
  assert.deepEqual(beta.jobs['deploy-testing'].permissions, {
    'id-token': 'write',
    contents: 'read',
    actions: 'read',
  });

  const readmeSync = pr.jobs['sync-readme-versions'];
  assert.match(readmeSync.if, /head\.repo\s*!=\s*null/);
  assert.deepEqual(readmeSync.permissions, { contents: 'read' });
  assert.equal(readmeSync.steps[0].with.repository, '${{ github.event.pull_request.head.repo.full_name }}');
  assert.equal(readmeSync.steps[0].with.ref, '${{ github.event.pull_request.head.ref }}');
  assert.equal(readmeSync.steps[0].with['persist-credentials'], false);
});

test('release workflows pin tool versions and record and verify complete build provenance', () => {
  for (const name of ['beta-release', 'stable-release']) {
    const { source, workflow } = WORKFLOWS[name];
    assert.equal(workflow.env.NODE_VERSION, '24.20.0');
    assert.equal(workflow.env.DOTNET_VERSION, '10.0.303');

    const build = workflow.jobs.build;
    const publish = build.steps.find((step) => step.id === 'publish');
    const deploy = Object.values(workflow.jobs)
      .flatMap((job) => job.steps ?? [])
      .find((step) => step.name === '🔒 Verify deployment artifact provenance');
    assert.ok(publish);
    assert.ok(deploy);

    for (const field of PROVENANCE_FIELDS) {
      assert.equal(build.outputs[field], `\${{ steps.publish.outputs.${field} }}`);
      assert.ok(publish.run.includes(`${field}=%s`), `${name} must write ${field} to the artifact`);
      const envName = `BUILD_${field.replaceAll('-', '_').toUpperCase()}`;
      assert.equal(deploy.env[envName], `\${{ needs.build.outputs.${field} }}`);
      assert.ok(deploy.run.includes(`"$${envName}"`), `${name} must compare ${field} during deployment`);
    }
    assert.match(publish.run, /dotnet --list-sdks/);
    assert.match(publish.run, /dotnet --list-runtimes/);
    assert.match(publish.run, /ImageVersion/);
    assert.match(source, /node-version: \$\{\{ env\.NODE_VERSION \}\}/);
  }
});

test('all workflow actions stay pinned to full commit SHAs with version comments', () => {
  for (const { source } of Object.values(WORKFLOWS)) {
    const actionLines = source.split('\n').filter((line) => /^\s+uses:/.test(line));
    assert.ok(actionLines.length > 0);
    for (const line of actionLines.map((line) => line.trimEnd())) {
      assert.match(line, /@[0-9a-f]{40}\s+#\s+v[\w.-]+$/);
    }
  }
});

test('badge preparation pushes a badge-only child before tagging the validated candidate', (context) => {
  const fixture = createRepository();
  context.after(() => rmSync(fixture.directory, { recursive: true, force: true }));
  const outputPath = join(fixture.directory, 'github-output.txt');
  writeFileSync(outputPath, '');

  const result = runBadgePrepare({ ...fixture, outputPath });
  assert.equal(result.status, 0, result.stderr || result.stdout);
  const branchSha = remoteBranchSha(fixture.remote);
  assert.equal(git(fixture.repo, ['rev-parse', 'HEAD']), fixture.validatedSha);
  assert.equal(branchSha, readFileSync(outputPath, 'utf8').match(/^release_branch_sha=(\w+)$/m)?.[1]);
  assert.equal(remoteGit(fixture.remote, ['rev-list', '--parents', '-n', '1', branchSha]).split(' ')[1], fixture.validatedSha);
  assert.deepEqual(
    remoteGit(fixture.remote, ['diff-tree', '--no-commit-id', '--name-only', '-r', branchSha]).split('\n'),
    ['badges/pre-release.png'],
  );

  git(fixture.repo, ['checkout', '--detach', '--force', branchSha]);
  const retry = runBadgePrepare({ ...fixture, outputPath });
  assert.equal(retry.status, 0, retry.stderr || retry.stdout);
  assert.equal(git(fixture.repo, ['rev-parse', 'HEAD']), fixture.validatedSha);
  assert.equal(remoteBranchSha(fixture.remote), branchSha);

  const tag = 'v3.2.1-alpha.1';
  git(fixture.repo, ['tag', tag, fixture.validatedSha]);
  git(fixture.repo, ['push', 'origin', `refs/tags/${tag}`]);
  assert.equal(remoteGit(fixture.remote, ['rev-parse', `refs/tags/${tag}^{commit}`]), fixture.validatedSha);
});

test('a rejected badge push aborts prepare and leaves the candidate untagged', (context) => {
  const fixture = createRepository();
  context.after(() => rmSync(fixture.directory, { recursive: true, force: true }));
  const hook = join(fixture.remote, 'hooks/pre-receive');
  writeFileSync(hook, '#!/bin/sh\nwhile read old new ref; do\n  if [ "$ref" = "refs/heads/main" ]; then exit 1; fi\ndone\nexit 0\n');
  chmodSync(hook, 0o755);
  const outputPath = join(fixture.directory, 'github-output.txt');
  writeFileSync(outputPath, '');

  const result = runBadgePrepare({ ...fixture, outputPath });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Badge branch push failed/);
  assert.equal(remoteBranchSha(fixture.remote), fixture.validatedSha);
  assert.equal(git(fixture.repo, ['tag', '--list']), '');
  assert.equal(remoteGit(fixture.remote, ['tag', '--list']), '');
  assert.equal(readFileSync(outputPath, 'utf8'), '');
});

test('badge generation failure prevents branch update and leaves the candidate untagged', (context) => {
  const fixture = createRepository();
  context.after(() => rmSync(fixture.directory, { recursive: true, force: true }));
  renameSync(join(fixture.repo, 'badges'), join(fixture.repo, 'badges-original'));
  writeFileSync(join(fixture.repo, 'badges'), 'not a directory');
  const outputPath = join(fixture.directory, 'github-output.txt');
  writeFileSync(outputPath, '');

  const result = runBadgePrepare({ ...fixture, outputPath });
  assert.notEqual(result.status, 0);
  assert.equal(remoteBranchSha(fixture.remote), fixture.validatedSha);
  assert.equal(git(fixture.repo, ['tag', '--list']), '');
  assert.equal(remoteGit(fixture.remote, ['tag', '--list']), '');
  assert.equal(readFileSync(outputPath, 'utf8'), '');
});

test('semantic-release runs badge publication in prepare before tagging', () => {
  const releaseConfig = readFileSync(join(ROOT, '.releaserc.mjs'), 'utf8');
  const badgeScript = readFileSync(SCRIPT, 'utf8');
  assert.match(releaseConfig, /prepareCmd:\s*'node scripts\/generate-badges\.mjs \$\{nextRelease\.version\} --publish'/);
  assert.match(badgeScript, /checkout', '--detach', '--force'/);
  for (const name of ['beta-release', 'stable-release']) {
    const workflow = WORKFLOWS[name].workflow;
    const releaseStep = workflow.jobs.release.steps.find((step) => step.id === 'semantic-release');
    assert.match(releaseStep.run, /set -euo pipefail/);
    assert.equal(releaseStep.env.BADGE_BRANCH, name === 'beta-release' ? 'main' : 'release');
    assert.doesNotMatch(releaseStep.run, /git (?:add|commit) badges/);
  }
});
