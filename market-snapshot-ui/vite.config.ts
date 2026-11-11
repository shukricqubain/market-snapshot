import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// One config for the dev server, the production build and Vitest.
export default defineConfig({
  plugins: [react()],
  // Fixed port: the API's CORS policy allows exactly http://localhost:5173.
  server: { port: 5173, strictPort: true },
  test: {
    environment: 'jsdom', // browser-like DOM for React Testing Library
    setupFiles: ['./src/test/setup.ts'],
  },
});
