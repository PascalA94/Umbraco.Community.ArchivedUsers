import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    lib: {
      entry: 'src/index.ts',
      formats: ['es'],
      fileName: () => 'index.js',
    },
    // Build straight into the RCL's wwwroot, served at /App_Plugins/ArchivedUsers/
    // via static web assets. umbraco-package.json is copied from public/.
    outDir: '../wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      // Never bundle core client code: the backoffice provides it at runtime (AU-15).
      external: [/^@umbraco-cms\/backoffice/],
      output: {
        chunkFileNames: '[name]-[hash].js',
      },
    },
  },
});
