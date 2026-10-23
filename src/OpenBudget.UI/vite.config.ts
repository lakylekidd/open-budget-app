import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const apiTarget = 'http://127.0.0.1:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Preserve the browser-facing host for the API's same-origin write checks.
      // Vite's string shorthand enables changeOrigin and replaces it with port 5080.
      '/api': { target: apiTarget, changeOrigin: false },
      '/health': { target: apiTarget, changeOrigin: false },
    },
  },
});
