/** @type {import('semantic-release').GlobalConfig} */
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
          types: [
            { type: 'feat', section: '✨ Features' },
            { type: 'fix', section: '🐛 Bug Fixes' },
            { type: 'perf', section: '⚡ Performance Improvements' },
            { type: 'revert', section: '⏪ Reverts' },
            { type: 'style', section: '💄 Styles' },
            { type: 'refactor', section: '♻️ Code Refactoring' },
            { type: 'build', section: '🔧 Build System' },
            { type: 'chore', section: '🔨 Chores' },
            { type: 'test', hidden: true },
            { type: 'docs', hidden: true },
            { type: 'ci', hidden: true },
          ],
        },
        writerOpts: {
          commitsSort: ['type', 'scope', 'subject'],
          transform: (commit) => {
            if (!commit.body) return commit;
            const body = commit.body
              // Join lines that are a continuation inside a table cell (no | at start/end)
              .replace(/\|\n([^|\-\n])/g, '| $1')
              // Join lines where content wraps before a closing |
              .replace(/([^|\n])\n\|/g, '$1 |')
              // Clean up any remaining wrapped content between pipes
              .replace(/\|\n\|/g, '| |');
            return { ...commit, body };
          },
          commitPartial:
            '* {{#if scope}}**{{scope}}:** {{/if}}{{subject}}' +
            '{{#if hash}} ([{{shortHash}}]({{@root.host}}/{{@root.owner}}/{{@root.repository}}/commit/{{hash}})){{/if}}\n\n' +
            '{{#if body}}<details><summary>Details</summary>\n\n{{body}}\n\n</details>\n{{/if}}' +
            '{{#if notes}}\n\n{{#each notes}}### {{title}}\n\n{{text}}\n\n{{/each}}{{/if}}',
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
        prepareCmd: 'node scripts/generate-badges.mjs ${nextRelease.version}',
      },
    ],
    '@semantic-release/github',
  ],
};
