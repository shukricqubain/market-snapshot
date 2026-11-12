import { useRef, useState } from 'react';
import DailySummaryTable from './components/DailySummaryTable';
import SymbolSearchForm from "./components/SymbolSearchForm";
import type { DailySummary } from './api/types';
import { fetchDailySummaries } from './api/dailySummaries';

export default function App() {

  const [rows, setRows] = useState<DailySummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const controllerRef = useRef<AbortController | null>(null);

  async function handleSearch(symbol: string) {
    // Cancel any search still in flight, so a slow old response can't overwrite this one.
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;

    setLoading(true);
    setError(null);
    setRows(null);

    try {
      setRows(await fetchDailySummaries(symbol, controller.signal));
    } catch (err) {
      // An abort only means a newer search replaced this one: not an error to show.
      if (err instanceof Error && err.name === 'AbortError') return;
      setError(err instanceof Error ? err.message : 'Something went wrong.');
    } finally {
      // Only the latest search may switch loading off; a superseded one must not.
      if (controllerRef.current === controller) setLoading(false);
    }
  }

  return (
    <main>
      <h1>Market Snapshot</h1>
      <SymbolSearchForm onSearch={handleSearch} />
      {loading && <p role="status">Loading…</p>}
      {error && <p role="alert">{error}</p>}
      {rows && <DailySummaryTable rows={rows} />}
    </main>
  );
}
