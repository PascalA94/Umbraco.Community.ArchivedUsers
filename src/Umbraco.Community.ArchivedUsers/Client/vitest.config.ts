import { defineConfig } from 'vitest/config';

// Pure modules run in Node; files that render Lit elements opt into happy-dom per file.
export default defineConfig({
  test: {
    include: ['test/**/*.test.ts'],
    environment: 'node',
  },
});
