import { setupServer } from 'msw/node';
import { handlers } from './handlers';

// Intercepts fetch in tests at the network level, so the real fetch code runs unchanged.
export const server = setupServer(...handlers);
