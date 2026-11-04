import { defineConfig } from '@hey-api/openapi-ts';

// Generates the typed client from the package's own OpenAPI document (docs/spikes.md S6).
// Run the demo site first (dotnet run --project demo/Umbraco.Community.ArchivedUsers.DemoSite), then `npm run generate:api`.
export default defineConfig({
  input: process.env.ARCHIVED_USERS_OPENAPI ?? 'http://localhost:28331/umbraco/openapi/archived-users.json',
  output: { path: 'src/api/generated', format: false },
  plugins: ['@hey-api/client-fetch', '@hey-api/sdk', '@hey-api/typescript'],
});
