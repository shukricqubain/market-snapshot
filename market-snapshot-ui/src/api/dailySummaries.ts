import { dailySummariesUrl } from './config';
import type { DailySummary, ProblemDetails } from './types';
export async function fetchDailySummaries(symbol: string, signal?: AbortSignal): Promise<DailySummary[]> {
  let response: Response;
  try {
    response = await fetch(dailySummariesUrl(symbol), { signal });
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error;
    throw new Error('Could not reach the server.');
  }
  if (response.ok) {
    return await response.json() as DailySummary[];
  } else if (response.status >= 500) {
    throw new Error('Market data is temporarily unavailable. Please try again.')
  } else {
    let problem: ProblemDetails | undefined;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // body wasn't JSON; fall back to the generic message below
    }
    throw new Error(problem?.detail ?? 'Request failed.');
  }
}
