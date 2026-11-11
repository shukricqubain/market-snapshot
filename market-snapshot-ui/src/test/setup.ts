import '@testing-library/jest-dom/vitest'; // adds matchers like toBeInTheDocument()
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './server';

// Fail loudly on any request without a handler (MSW 3 renamed onUnhandledRequest to onUnhandledFrame), so no test can silently hit a real server.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }));
afterEach(() => {
  server.resetHandlers(); // drop per-test overrides
  cleanup();              // unmount rendered components (needed because Vitest globals are off)
});
afterAll(() => server.close());
