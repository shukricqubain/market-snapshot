import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import App from './App';
import { server } from './test/server';
import { dailySummariesRoute, networkErrorResponse, problemResponse } from './test/handlers';

// Contract for App (queried the way a user or screen reader finds things):
// - a text input labelled "Stock symbol" and a "Search" button, disabled until a symbol is typed
// - submitting (button or Enter) trims the input and fetches that symbol
// - while loading: an element with role "status" containing "Loading"
// - on success: a table with columns Day / Low average / High average / Volume;
//   prices shown with exactly 4 decimals, volume with thousands separators
// - no rows: a "No data" message instead of an empty table
// - on failure: an element with role "alert" containing the error message
// - a new search clears the previous error or results
describe('App', () => {
  const setup = () => {
    const user = userEvent.setup();
    render(<App />);
    return {
      user,
      input: screen.getByLabelText('Stock symbol'),
      search: screen.getByRole('button', { name: 'Search' }),
    };
  };

  it('renders the heading', () => {
    render(<App />);
    expect(screen.getByRole('heading', { name: 'Market Snapshot' })).toBeInTheDocument();
  });

  it('disables Search until a symbol is entered', async () => {
    const { user, input, search } = setup();

    expect(search).toBeDisabled();
    await user.type(input, '   ');
    expect(search).toBeDisabled(); // whitespace alone isn't a symbol
    await user.type(input, 'TSLA');
    expect(search).toBeEnabled();
  });

  it('shows the daily summaries in a table', async () => {
    const { user, input, search } = setup();

    await user.type(input, 'TSLA');
    await user.click(search);

    const table = await screen.findByRole('table');
    const headers = within(table).getAllByRole('columnheader').map((h) => h.textContent);
    expect(headers).toEqual(['Day', 'Low average', 'High average', 'Volume']);

    const rows = within(table).getAllByRole('row').slice(1); // skip the header row
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      '2026-10-12',
      '378.7496',
      '379.8398',
      '17,558,067',
    ]);
  });

  it('shows prices with exactly 4 decimals', async () => {
    server.use(
      http.get(dailySummariesRoute, () =>
        HttpResponse.json([{ day: '2026-10-12', lowAverage: 10.5, highAverage: 12, volume: 300 }]),
      ),
    );
    const { user, input, search } = setup();

    await user.type(input, 'TSLA');
    await user.click(search);

    const row = (await screen.findAllByRole('row'))[1];
    expect(within(row).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      '2026-10-12',
      '10.5000',
      '12.0000',
      '300',
    ]);
  });

  it('submits with Enter and trims the symbol', async () => {
    let requestedSymbol = '';
    server.use(
      http.get(dailySummariesRoute, ({ params }) => {
        requestedSymbol = String(params.symbol);
        return HttpResponse.json([]);
      }),
    );
    const { user, input } = setup();

    await user.type(input, '  TSLA  {Enter}');

    await screen.findByText(/no data/i);
    expect(requestedSymbol).toBe('TSLA');
  });

  it('shows a loading state while the request is in flight', async () => {
    let release!: () => void;
    const gate = new Promise<void>((resolve) => (release = resolve));
    server.use(
      http.get(dailySummariesRoute, async () => {
        await gate;
        return HttpResponse.json([]);
      }),
    );
    const { user, input, search } = setup();

    await user.type(input, 'TSLA');
    await user.click(search);

    expect(await screen.findByRole('status')).toHaveTextContent(/loading/i);
    release();
    await screen.findByText(/no data/i);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('shows a message instead of a table when there is no data', async () => {
    server.use(http.get(dailySummariesRoute, () => HttpResponse.json([])));
    const { user, input, search } = setup();

    await user.type(input, 'TSLA');
    await user.click(search);

    expect(await screen.findByText(/no data/i)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows the API message for an unknown symbol', async () => {
    server.use(problemResponse(404, "Symbol 'ZZZZ' was not found."));
    const { user, input, search } = setup();

    await user.type(input, 'ZZZZ');
    await user.click(search);

    expect(await screen.findByRole('alert')).toHaveTextContent("Symbol 'ZZZZ' was not found.");
  });

  it('shows a friendly message when the upstream is unavailable', async () => {
    server.use(problemResponse(502, 'Yahoo Finance timed out.'));
    const { user, input, search } = setup();

    await user.type(input, 'TSLA');
    await user.click(search);

    expect(await screen.findByRole('alert')).toHaveTextContent(/temporarily unavailable/i);
  });

  it('shows a friendly message when the API cannot be reached', async () => {
    server.use(networkErrorResponse());
    const { user, input, search } = setup();

    await user.type(input, 'TSLA');
    await user.click(search);

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not reach/i);
  });

  it('clears a previous error when a new search succeeds', async () => {
    server.use(
      http.get(
        dailySummariesRoute,
        () => HttpResponse.json({ status: 404, detail: "Symbol 'ZZZZ' was not found." }, { status: 404 }),
        { once: true }, // only the first request fails; later ones fall back to the default success handler
      ),
    );
    const { user, input, search } = setup();

    await user.type(input, 'ZZZZ');
    await user.click(search);
    await screen.findByRole('alert');

    await user.clear(input);
    await user.type(input, 'TSLA');
    await user.click(search);

    await screen.findByRole('table');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
