export default {
  extends: ['@commitlint/config-conventional'],
  ignores: [
    message =>
      process.env.GITHUB_EVENT_NAME === 'pull_request' &&
      !/^[a-z]+(\([a-z0-9-]+\))?!?:\s.+/.test(message.trim()),
  ],
  rules: {
    // Type rules
    'type-enum': [
      2,
      'always',
      [
        'feat',
        'fix',
        'docs',
        'style',
        'refactor',
        'perf',
        'test',
        'build',
        'ci',
        'chore',
        'revert',
      ],
    ],
    'type-case': [2, 'always', 'lower-case'],
    'type-empty': [2, 'never'],

    // Scope rules
    'scope-case': [2, 'always', 'lower-case'],
    'scope-enum': [
      2,
      'always',
      [
        // Backend projects (RefTestManagement.*)
        'api',
        'application',
        'domain',
        'infrastructure',
        'security',
        'auth0',
        'auditlog',

        // Backend domains / GraphQL mutation areas
        'reftests',
        'lifecycle',
        'creation',
        'approval',
        'reset',
        'email',
        'jobs',
        'expiration',

        // Frontend (RefTestManagement.Ui)
        'ui',
        'graphql',
        'i18n',
        'pwa',

        // Cross-cutting / infra
        'deps',
        'config',
        'release',
        'docs',
      ],
    ],

    // Subject rules
    'subject-case': [
      2,
      'never',
      ['sentence-case', 'start-case', 'pascal-case', 'upper-case'],
    ],
    'subject-empty': [2, 'never'],
    'subject-full-stop': [2, 'never', '.'],

    // Header rules
    'header-max-length': [2, 'always', 100],

    // Body rules
    'body-leading-blank': [2, 'always'],
    'body-max-line-length': [2, 'always', 100],

    // Footer rules
    'footer-leading-blank': [2, 'always'],
    'footer-max-line-length': [2, 'always', 100],
  },
};
