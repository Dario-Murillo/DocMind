export const environment = {
  // Same origin in production: nginx serves the app and forwards /api/ to the API, stripping the
  // prefix. Without it, API routes such as /notebooks/{id} would clash with the app's own routes.
  apiBaseUrl: '/api',
};
