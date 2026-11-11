import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import App from './App';

// Smoke test for the test setup itself (jsdom, RTL, jest-dom matchers, MSW lifecycle).
describe('App', () => {
  it('renders the heading', () => {
    render(<App />);
    expect(screen.getByRole('heading', { name: 'Market Snapshot' })).toBeInTheDocument();
  });
});
