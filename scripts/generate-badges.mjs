#!/usr/bin/env node
import { execFileSync } from 'node:child_process';
import { appendFileSync, copyFileSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { makeBadge } from 'badge-maker';
import sharp from 'sharp';

const BADGE_FILES = ['badges/release.png', 'badges/pre-release.png'];
const BADGE_BRANCHES = new Set(['main', 'release']);

function git(cwd, args) {
  try {
    return execFileSync('git', args, { cwd, encoding: 'utf8' });
  } catch (error) {
    const details = error.stderr?.toString().trim();
    throw new Error(`git ${args[0]} failed${details ? `: ${details}` : ''}`, { cause: error });
  }
}

export async function generateBadges(version, badgesDir = join(process.cwd(), 'badges')) {
  if (!version) throw new Error('Usage: node generate-badges.mjs <version> [--publish]');

  // This transparent PNG hides an inactive badge without showing a broken image.
  const transparentPng = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQI12NgAAIABQAABjkB6QAAAABJRU5ErkJggg==',
    'base64',
  );
  const svgToPng = (svg) => sharp(Buffer.from(svg), { density: 1200 }).png().toBuffer();
  const isPreRelease = version.includes('-');

  mkdirSync(badgesDir, { recursive: true });
  if (isPreRelease) {
    const png = await svgToPng(makeBadge({
      label: 'pre-release',
      message: `v${version}`,
      color: 'orange',
      style: 'flat',
    }));
    writeFileSync(join(badgesDir, 'pre-release.png'), png);
    console.log(`Generated pre-release badge: v${version}`);
  } else {
    const png = await svgToPng(makeBadge({
      label: 'release',
      message: `v${version}`,
      color: '0075ca',
      style: 'flat',
    }));
    writeFileSync(join(badgesDir, 'release.png'), png);
    writeFileSync(join(badgesDir, 'pre-release.png'), transparentPng);
    console.log(`Generated release badge: v${version}`);
    console.log('Hid pre-release badge (no active pre-release)');
  }
}

function getRemoteBranchSha(repoDir, branch) {
  const ref = `refs/heads/${branch}`;
  const matches = git(repoDir, ['ls-remote', '--heads', 'origin', ref])
    .trim()
    .split('\n')
    .filter(Boolean)
    .map((line) => line.split(/\s+/))
    .filter(([, remoteRef]) => remoteRef === ref);
  if (matches.length > 1) throw new Error(`Remote branch ${branch} resolved ambiguously.`);
  return matches[0]?.[0] ?? null;
}

function matchesPreparedBadgeCommit(repoDir, branch, remoteSha, validatedSha, expectedTree) {
  if (!remoteSha) return false;
  git(repoDir, ['fetch', '--no-tags', 'origin', `refs/heads/${branch}`]);
  if (git(repoDir, ['rev-parse', 'FETCH_HEAD']).trim() !== remoteSha) return false;
  const parents = git(repoDir, ['rev-list', '--parents', '-n', '1', remoteSha]).trim().split(/\s+/);
  return parents.length === 2 &&
    parents[1] === validatedSha &&
    git(repoDir, ['rev-parse', `${remoteSha}^{tree}`]).trim() === expectedTree;
}

export function publishBadgeCommit({ repoDir, badgesDir, branch, validatedSha, githubOutputPath, version }) {
  if (!BADGE_BRANCHES.has(branch)) throw new Error(`Unsupported badge branch: ${branch}`);
  if (!/^[0-9a-f]{40}$/.test(validatedSha ?? '')) throw new Error('A validated 40-character commit SHA is required.');
  if (!githubOutputPath) throw new Error('GITHUB_OUTPUT is required to publish the badge branch SHA.');

  const root = resolve(git(repoDir, ['rev-parse', '--show-toplevel']).trim());
  const checkoutSha = git(root, ['rev-parse', 'HEAD']).trim();
  const remoteSha = getRemoteBranchSha(root, branch);
  if (!remoteSha) throw new Error(`Remote branch ${branch} does not exist.`);
  if (checkoutSha !== validatedSha && checkoutSha !== remoteSha) {
    throw new Error('The release checkout is neither the validated candidate nor the current remote branch.');
  }

  const worktreeParent = mkdtempSync(join(tmpdir(), 'release-badges-'));
  const worktree = join(worktreeParent, 'worktree');
  let worktreeAdded = false;
  try {
    git(root, ['worktree', 'add', '--detach', worktree, validatedSha]);
    worktreeAdded = true;
    for (const file of BADGE_FILES) {
      copyFileSync(join(badgesDir, file.split('/')[1]), join(worktree, file));
    }
    git(worktree, ['add', '--', ...BADGE_FILES]);
    const changedFiles = git(worktree, ['diff', '--cached', '--name-only'])
      .trim()
      .split('\n')
      .filter(Boolean);
    if (changedFiles.some((file) => !BADGE_FILES.includes(file))) {
      throw new Error('The badge commit contains unexpected files.');
    }

    if (!changedFiles.length) {
      if (remoteSha !== validatedSha) {
        throw new Error('The remote branch moved and no badge-only commit is available to retry.');
      }
      appendFileSync(githubOutputPath, `release_branch_sha=${validatedSha}\n`);
      return validatedSha;
    }

    git(worktree, [
      '-c', 'user.name=github-actions[bot]',
      '-c', 'user.email=github-actions[bot]@users.noreply.github.com',
      'commit', '-m', `ci(release): update badges to v${version} [skip ci]`,
    ]);
    const badgeSha = git(worktree, ['rev-parse', 'HEAD']).trim();
    const parents = git(worktree, ['rev-list', '--parents', '-n', '1', badgeSha]).trim().split(/\s+/);
    if (parents.length !== 2 || parents[1] !== validatedSha) {
      throw new Error('The badge commit is not a direct child of the validated candidate.');
    }
    const badgeTree = git(worktree, ['rev-parse', 'HEAD^{tree}']).trim();

    if (remoteSha !== validatedSha) {
      if (!matchesPreparedBadgeCommit(root, branch, remoteSha, validatedSha, badgeTree)) {
        throw new Error('The remote branch is not the validated candidate or its expected badge-only commit.');
      }
      appendFileSync(githubOutputPath, `release_branch_sha=${remoteSha}\n`);
      return remoteSha;
    }

    // Keep the semantic-release checkout on the validated commit; this non-forced push is the final race check.
    try {
      git(root, ['push', 'origin', `${badgeSha}:refs/heads/${branch}`]);
    } catch (error) {
      const currentRemoteSha = getRemoteBranchSha(root, branch);
      if (matchesPreparedBadgeCommit(root, branch, currentRemoteSha, validatedSha, badgeTree)) {
        appendFileSync(githubOutputPath, `release_branch_sha=${currentRemoteSha}\n`);
        return currentRemoteSha;
      }
      throw new Error(`Badge branch push failed: ${error.message}`, { cause: error });
    }

    const updatedRemoteSha = getRemoteBranchSha(root, branch);
    if (updatedRemoteSha !== badgeSha) {
      if (!matchesPreparedBadgeCommit(root, branch, updatedRemoteSha, validatedSha, badgeTree)) {
        throw new Error('The remote badge branch moved unexpectedly after the push.');
      }
    }
    appendFileSync(githubOutputPath, `release_branch_sha=${updatedRemoteSha}\n`);
    return updatedRemoteSha;
  } finally {
    if (worktreeAdded) git(root, ['worktree', 'remove', '--force', worktree]);
    rmSync(worktreeParent, { recursive: true, force: true });
  }
}

async function main() {
  const [, , version, option, ...extra] = process.argv;
  if (!version || extra.length || (option && option !== '--publish')) {
    throw new Error('Usage: node generate-badges.mjs <version> [--publish]');
  }
  const repoDir = process.cwd();
  const root = resolve(git(repoDir, ['rev-parse', '--show-toplevel']).trim());
  const badgesDir = join(root, 'badges');
  await generateBadges(version, badgesDir);
  if (option === '--publish') {
    publishBadgeCommit({
      repoDir: root,
      badgesDir,
      branch: process.env.BADGE_BRANCH,
      validatedSha: process.env.VALIDATED_SHA,
      githubOutputPath: process.env.GITHUB_OUTPUT,
      version,
    });
    // A retry starts at the existing badge commit; restore the validated candidate before semantic-release tags.
    if (git(root, ['rev-parse', 'HEAD']).trim() !== process.env.VALIDATED_SHA) {
      git(root, ['checkout', '--detach', '--force', process.env.VALIDATED_SHA]);
    }
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
