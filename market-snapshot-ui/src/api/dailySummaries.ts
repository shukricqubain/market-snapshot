import { dailySummariesUrl } from './config';
import type { DailySummary } from './types';

// TODO: fetch dailySummariesUrl(symbol) (pass `signal` so a newer search can cancel this one).
//       ok -> return the parsed DailySummary[]
//       not ok -> read the ProblemDetails body and throw an error the UI can show
//                 (the 404 detail names the bad symbol; 502 means Yahoo is unavailable)
//       network failure (fetch itself rejects, e.g. API not running) -> a friendly message too
export async function fetchDailySummaries(symbol: string, signal?: AbortSignal): Promise<DailySummary[]> {
  void dailySummariesUrl(symbol);
  void signal;
  throw new Error('Not implemented');
}
