/** @type {import('semantic-release').GlobalConfig} */

const PRESET_TYPES = [
  { type: 'feat', section: '✨ Features' },
  { type: 'fix', section: '🐛 Bug Fixes' },
  { type: 'perf', section: '⚡ Performance Improvements' },
  { type: 'revert', section: '⏪ Reverts' },
  { type: 'style', section: '💄 Styles' },
  { type: 'refactor', section: '♻️ Code Refactoring' },
  { type: 'build', section: '🔧 Build System' },
  { type: 'chore', section: '🔨 Chores' },
  { type: 'test', effect: 'hidden' },
  { type: 'docs', effect: 'hidden' },
  { type: 'ci', effect: 'hidden' },
];

function fixTableBody(body) {
  return (
    body
      // Join wrapped content after a pipe (but not before separator rows like |---|)
      .replace(/\|\n(?![|\-])/g, '| ')
      // Join wrapped content before a pipe (but not after separator rows)
      .replace(/(?<![|\-])\n\|/g, ' |')
      // Join remaining wrapped lines inside a cell (no pipe on either side, not a list item)
      .replace(/([^|\n])\n(?![-\s*]|$)([^|\n])/g, '$1 $2')
  );
}

function indentBody(body) {
  return body
    .split('\n')
    .map((line) => '  ' + line)
    .join('\n');
}

export default {
  branches: [
    'release',
    {
      name: 'main',
      prerelease: 'alpha',
    },
  ],
  tagFormat: 'v${version}',
  plugins: [
    [
      '@semantic-release/commit-analyzer',
      {
        preset: 'conventionalcommits',
        releaseRules: [
          { breaking: true, release: 'major' },
          { type: 'feat', release: 'minor' },
          { type: 'fix', release: 'patch' },
          { type: 'perf', release: 'patch' },
          { type: 'revert', release: 'patch' },
          { type: 'style', release: 'patch' },
          { type: 'refactor', release: 'patch' },
          { type: 'build', release: 'patch' },
          { type: 'chore', release: 'patch' },
          { type: 'test', release: false },
          { type: 'docs', release: false },
          { type: 'ci', release: false },
        ],
      },
    ],
    [
      '@semantic-release/release-notes-generator',
      {
        preset: 'conventionalcommits',
        presetConfig: {
          types: PRESET_TYPES,
        },
        writerOpts: {
          commitsSort: ['type', 'scope', 'subject'],
          transform: (commit) => {
            // Filter out merge commits
            if (commit.merge) return false;
            const typeConfig = PRESET_TYPES.find((t) => t.type === commit.type);
            // Filter out hidden types and commits with no matching type
            if (!typeConfig || typeConfig.effect === 'hidden') return false;
            return {
              ...commit,
              type: typeConfig?.section ?? commit.type,
              shortHash: commit.hash ? commit.hash.slice(0, 7) : '',
              body:
                commit.body ?
                  indentBody(fixTableBody(commit.body))
                : commit.body,
            };
          },
          template: (context) => {
            const groups = context.commitGroups.map(({ title, commits }) => {
              const heading = title ? `### ${title}` : '';
              const entries = commits
                .map((commit) => context.commitPartial(context, commit))
                .join('\n\n');
              return [heading, entries].filter(Boolean).join('\n\n');
            });
            return [
              context.headerPartial(context),
              ...groups,
              context.footerPartial(context),
            ].filter(Boolean).join('\n\n');
          },
          commitPartial: (context, commit) => {
            const scope = commit.scope ? `**${commit.scope}:** ` : '';
            const link =
              commit.hash ?
                ` ([${commit.shortHash}](${context.host}/${context.owner}/${context.repository}/commit/${commit.hash}))`
              : '';
            const body =
              commit.body ?
                `\n\n  <details><summary>Details</summary>\n\n${commit.body}\n\n  </details>`
              : '';
            const notes =
              commit.notes?.length ?
                `\n\n${commit.notes.map(({ title, text }) => `### ${title}\n\n${text}`).join('\n\n')}`
              : '';
            return `* ${scope}${commit.subject ?? ''}${link}${body}${notes}`;
          },
        },
      },
    ],
    [
      '@semantic-release/npm',
      {
        npmPublish: false,
      },
    ],
    [
      '@semantic-release/exec',
      {
        prepareCmd: 'node scripts/generate-badges.mjs ${nextRelease.version} --publish',
      },
    ],
    '@semantic-release/github',
  ],
};
