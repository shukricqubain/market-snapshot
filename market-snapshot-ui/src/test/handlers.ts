import { http, HttpResponse } from 'msw';
import { API_BASE_URL } from '../api/config';
import type { DailySummary, ProblemDetails } from '../api/types';

// Same route the real API serves; ':symbol' matches any symbol.
export const dailySummariesRoute = `${API_BASE_URL}/api/stocks/:symbol/daily-summaries`;

export const sampleSummaries: DailySummary[] = [
  { day: '2026-10-12', lowAverage: 378.7496, highAverage: 379.8398, volume: 17558067 },
  { day: '2026-10-13', lowAverage: 374.4611, highAverage: 375.5825, volume: 21744117 },
];

// Default: every request succeeds. Override per test with server.use(...) and the helpers below.
export const handlers = [http.get(dailySummariesRoute, () => HttpResponse.json(sampleSummaries))];

// Returns a ProblemDetails error response, shaped like the API's.
export const problemResponse = (status: number, detail: string) =>
  http.get(dailySummariesRoute, () =>
    HttpResponse.json<ProblemDetails>({ status, title: 'Error', detail }, { status }),
  );

// Simulates the API being unreachable (fetch rejects, no response at all).
export const networkErrorResponse = () => http.get(dailySummariesRoute, () => HttpResponse.error());
