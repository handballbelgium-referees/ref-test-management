import type { CodegenConfig } from '@graphql-codegen/cli';

const config: CodegenConfig = {
  overwrite: true,
  schema: {
    'https://localhost:7039/graphql': {
      headers: {},
    },
  },
  documents: ['graphql/**/*.graphql'],
  generates: {
    'graphql/generated.ts': {
      plugins: ['typescript', 'typescript-operations', 'typescript-apollo-angular'],
      config: {
        addExplicitOverride: true,
        strictScalars: true,
        scalars: {
          UUID: 'string',
          DateTime: 'string',
          JSON: 'Record<string, string>',
        },
      },
    },
  },
};

export default config;
