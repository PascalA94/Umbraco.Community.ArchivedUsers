import { defineConfig } from '@hey-api/openapi-ts';

// Generates the typed client from the package's own OpenAPI document (docs/spikes.md S6); v17 line: Swashbuckle path.
// Run the demo site first (dotnet run --project demo/Umbraco.Community.ArchivedUsers.DemoSite), then `npm run generate:api`.
export default defineConfig({
  input: process.env.ARCHIVED_USERS_OPENAPI ?? 'http://localhost:28331/umbraco/swagger/archived-users/swagger.json',
  output: { path: 'src/api/generated', format: false },
  plugins: ['@hey-api/client-fetch', '@hey-api/sdk', '@hey-api/typescript'],
});
