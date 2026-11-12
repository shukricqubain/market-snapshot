import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../test/server';
import { dailySummariesRoute, networkErrorResponse, problemResponse, sampleSummaries } from '../test/handlers';
import { fetchDailySummaries } from './dailySummaries';

// Contract for fetchDailySummaries:
// - 200: resolves with the parsed DailySummary[]
// - 4xx with a ProblemDetails body: rejects with an Error whose message is the API's `detail`
//   (the API's 400 and 404 details are written for users, e.g. "Symbol 'ZZZZ' was not found.")
// - 5xx: rejects with a friendly "temporarily unavailable" message (the 502 detail is an internal
//   upstream reason like "Yahoo Finance timed out.", not something to show users)
// - fetch itself fails (API not running): rejects with a "could not reach" message
// - aborted by the caller: rejects with the AbortError untouched, so the UI can ignore it
describe('fetchDailySummaries', () => {
  it('resolves with the daily summaries on success', async () => {
    await expect(fetchDailySummaries('TSLA')).resolves.toEqual(sampleSummaries);
  });

  it('requests the URL-encoded symbol', async () => {
    let requestedPath = '';
    server.use(
      http.get(dailySummariesRoute, ({ request }) => {
        requestedPath = new URL(request.url).pathname;
        return HttpResponse.json([]);
      }),
    );

    await fetchDailySummaries('^GSPC');

    expect(requestedPath).toBe('/api/stocks/%5EGSPC/daily-summaries');
  });

  it("rejects with the API's detail for an unknown symbol (404)", async () => {
    server.use(problemResponse(404, "Symbol 'ZZZZ' was not found."));

    await expect(fetchDailySummaries('ZZZZ')).rejects.toThrow("Symbol 'ZZZZ' was not found.");
  });

  it("rejects with the API's detail for an invalid symbol (400)", async () => {
    server.use(problemResponse(400, 'Invalid Symbol was provided'));

    await expect(fetchDailySummaries('TS LA')).rejects.toThrow('Invalid Symbol was provided');
  });

  it('rejects with a friendly message when the upstream is unavailable (502)', async () => {
    server.use(problemResponse(502, 'Yahoo Finance timed out.'));

    await expect(fetchDailySummaries('TSLA')).rejects.toThrow(/temporarily unavailable/i);
  });

  // A 5xx body may not be JSON at all; that must not surface as a JSON parse error.
  it('rejects with a friendly message when a server error has a non-JSON body', async () => {
    server.use(http.get(dailySummariesRoute, () => new HttpResponse('Internal Server Error', { status: 500 })));

    await expect(fetchDailySummaries('TSLA')).rejects.toThrow(/temporarily unavailable/i);
  });

  it('rejects with a friendly message when the API cannot be reached', async () => {
    server.use(networkErrorResponse());

    await expect(fetchDailySummaries('TSLA')).rejects.toThrow(/could not reach/i);
  });

  it('lets an abort through unchanged so the caller can ignore it', async () => {
    const controller = new AbortController();
    controller.abort();

    await expect(fetchDailySummaries('TSLA', controller.signal)).rejects.toMatchObject({ name: 'AbortError' });
  });
});
