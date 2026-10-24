import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import yaml from 'js-yaml';
import { fileURLToPath } from 'node:url';
import releaseConfig from '../.releaserc.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const SCRIPT = join(ROOT, 'scripts/generate-badges.mjs');
const WORKFLOWS = Object.fromEntries(
  [
    'beta-release',
    'stable-release',
    'pr',
    'codeql',
    'copilot-setup-steps',
  ].map((name) => {
    const path = join(ROOT, `.github/workflows/${name}.yml`);
    const source = readFileSync(path, 'utf8');
    return [name, { source, workflow: yaml.load(source) }];
  }),
);

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
  assert.equal(stable.jobs['deploy-production'].environment.name, 'production');
  assert.equal(beta.jobs['deploy-testing'].environment.name, 'testing');
  for (const [workflowEntry, appServiceJob] of [
    [WORKFLOWS['beta-release'], 'deploy-testing'],
    [WORKFLOWS['stable-release'], 'deploy-production'],
  ]) {
    const { source, workflow } = workflowEntry;
    assert.equal(workflow.jobs[appServiceJob].if, "needs.release.outputs.new-release-published == 'true'");
    assert.equal(workflow.jobs.validate.outputs['validated-sha'], '${{ steps.candidate.outputs.sha }}');
    assert.doesNotMatch(source, /cloudflare|CONTAINER_DEPLOYMENT_TARGET|terraform/i);
  }
  assert.equal(
    beta.jobs['deploy-testing'].steps.find((step) => step.name === '🚀 Deploy to Azure Web App (Testing)').uses,
    'azure/webapps-deploy@02a81bead70021f5284939794bcec79c271ab383',
  );
  assert.equal(
    stable.jobs['deploy-production'].steps.find((step) => step.name === '🚀 Deploy to Azure Web App (Production)').uses,
    'azure/webapps-deploy@02a81bead70021f5284939794bcec79c271ab383',
  );
  assert.doesNotMatch(
    `${WORKFLOWS['beta-release'].source}\n${WORKFLOWS['stable-release'].source}`,
    /az containerapp|wrangler deploy/,
  );

  const readmeSync = pr.jobs['sync-readme-versions'];
  assert.match(readmeSync.if, /head\.repo\s*!=\s*null/);
  assert.deepEqual(readmeSync.permissions, { contents: 'read' });
  assert.equal(readmeSync.steps[0].with.repository, '${{ github.event.pull_request.head.repo.full_name }}');
  assert.equal(readmeSync.steps[0].with.ref, '${{ github.event.pull_request.head.ref }}');
  assert.equal(readmeSync.steps[0].with['persist-credentials'], false);
});

test('Cloudflare hosting and Terraform configuration are removed', () => {
  for (const path of [
    '.github/workflows/azure-container-release.yml',
    '.github/workflows/infra-deploy.yml',
    '.github/workflows/cloudflare-container-release.yml',
    '.github/workflows/container-build.yml',
    'infra/terraform/README.md',
    'infra/terraform/bootstrap/main.tf',
    'infra/terraform/environments/testing/main.tf',
    'infra/terraform/environments/production/main.tf',
    'infra/terraform/modules/aca-platform/main.tf',
    'infra/terraform/cloudflare/bootstrap/main.tf',
    'infra/terraform/cloudflare/environments/testing/main.tf',
    'infra/terraform/cloudflare/environments/production/main.tf',
    'infra/terraform/modules/cloudflare-platform/main.tf',
    'infra/cloudflare/worker/wrangler.jsonc',
    'RefTestManagement.Hosting.Cloudflare/RefTestManagement.Hosting.Cloudflare.csproj',
  ]) {
    assert.equal(existsSync(join(ROOT, path)), false, `${path} must not be present`);
  }
});
test('PR does not publish SDK container archives or require cloud credentials', () => {
  const { source, workflow } = WORKFLOWS.pr;
  const publishJob = workflow.jobs.validate;
  const publish = publishJob.steps.find((step) => step.name === '📦 Validate SDK container publishing');

  assert.equal(publish, undefined);
  assert.deepEqual(workflow.permissions, { contents: 'read' });
  assert.doesNotMatch(source, /PublishContainer|ContainerArchiveOutputPath/);
  assert.doesNotMatch(publishJob.steps.map((step) => step.uses ?? '').join('\n'), /azure\/login/i);
  assert.doesNotMatch(source, /id-token:\s*write/);
  assert.equal(workflow.jobs['validate-infrastructure'], undefined);
  assert.doesNotMatch(source, /infra\/terraform|terraform .*apply/i);
});

test('all .NET SDK setup steps use the repository global.json', () => {
  for (const [workflowName, { workflow }] of Object.entries(WORKFLOWS)) {
    const setupSteps = Object.values(workflow.jobs ?? {}).flatMap((job) =>
      (job.steps ?? []).filter((step) => step.uses?.startsWith('actions/setup-dotnet@')),
    );
    assert.ok(setupSteps.length > 0, `${workflowName} must set up the .NET SDK`);
    for (const step of setupSteps) {
      assert.equal(
        step.with?.['global-json-file'],
        'global.json',
        `${workflowName} must install the SDK declared by global.json`,
      );
      assert.equal(
        step.with?.['dotnet-version'],
        undefined,
        `${workflowName} must not pin a separate SDK version`,
      );
    }
  }
});

test('all workflow actions stay pinned to full commit SHAs with version comments', () => {
  for (const { source } of Object.values(WORKFLOWS)) {
    const actionLines = source
      .split('\n')
      .filter((line) => /^\s+uses:/.test(line) && !line.includes('./.github/workflows/'));
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

test('semantic-release runs badge publication before GitHub release publishing', () => {
  const execPluginIndex = releaseConfig.plugins.findIndex(
    (plugin) => Array.isArray(plugin) && plugin[0] === '@semantic-release/exec',
  );
  const githubPluginIndex = releaseConfig.plugins.indexOf('@semantic-release/github');
  assert.notEqual(execPluginIndex, -1);
  assert.ok(execPluginIndex < githubPluginIndex);
  assert.equal(
    releaseConfig.plugins[execPluginIndex][1].prepareCmd,
    'node scripts/generate-badges.mjs ${nextRelease.version} --publish',
  );

  const badgeScript = readFileSync(SCRIPT, 'utf8');
  assert.match(badgeScript, /if \(option === '--publish'\) \{\s+publishBadgeCommit\(\{/);
  assert.match(badgeScript, /checkout', '--detach', '--force'/);
  assert.match(badgeScript, /BADGE_BRANCH is required for --publish\./);
  for (const name of ['beta-release', 'stable-release']) {
    const workflow = WORKFLOWS[name].workflow;
    const releaseStep = workflow.jobs.release.steps.find((step) => step.id === 'semantic-release');
    assert.match(releaseStep.run, /set -euo pipefail/);
    assert.equal(releaseStep.env.BADGE_BRANCH, name === 'beta-release' ? 'main' : 'release');
    assert.doesNotMatch(releaseStep.run, /git (?:add|commit) badges/);
  }
});

test('CODEOWNERS and README match the active conditional semantic-release config', () => {
  const codeOwners = readFileSync(join(ROOT, '.github/CODEOWNERS'), 'utf8');
  const readme = readFileSync(join(ROOT, 'README.md'), 'utf8');
  const releaseConfig = readFileSync(join(ROOT, '.releaserc.mjs'), 'utf8');

  assert.match(codeOwners, /^\* @KristofGilis$/m);
  assert.match(codeOwners, /^\/\.releaserc\.mjs @KristofGilis$/m);
  assert.doesNotMatch(codeOwners, /^-\s+@KristofGilis$/m);
  assert.doesNotMatch(codeOwners, /^\/\.releaserc\.json\b/m);
  assert.match(
    readme,
    /Every push to `main` triggers the beta workflow, but semantic-release publishes a pre-release only when commit analysis finds a releasable change\./,
  );
  assert.match(releaseConfig, /'@semantic-release\/commit-analyzer'/);
  assert.match(releaseConfig, /releaseRules:/);
});
