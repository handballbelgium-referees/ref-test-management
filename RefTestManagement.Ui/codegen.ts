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
      plugins: ['typescript-operations', 'typescript-apollo-angular'],
      config: {
        addExplicitOverride: true,
        strictScalars: true,
        scalars: {
          Any: 'unknown',
          UUID: 'string',
          DateTime: 'string',
          LocalDate: 'string',
          TimeSpan: 'string',
          Long: 'number',
        },
      },
    },
  },
};

export default config;
