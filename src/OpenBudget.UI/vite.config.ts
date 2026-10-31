import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { themeColors } from './themeColors';

const apiTarget = 'http://127.0.0.1:5080';

export default defineConfig({
  plugins: [themeColors(), react()],
  server: {
    proxy: {
      // Preserve the browser-facing host for the API's same-origin write checks.
      // Vite's string shorthand enables changeOrigin and replaces it with port 5080.
      // Keys are anchored regexes so SPA deep links such as /apiK3y.../plan are never proxied (budget keys are
      // arbitrary base64url text); every other path falls back to index.html for client-side routing.
      '^/api(?:[/?]|$)': { target: apiTarget, changeOrigin: false },
      '^/health(?:[?]|$)': { target: apiTarget, changeOrigin: false },
      '^/mcp(?:[/?]|$)': { target: apiTarget, changeOrigin: false },
    },
  },
});
