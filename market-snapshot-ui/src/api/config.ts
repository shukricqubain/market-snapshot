// Base URL of the .NET API. Override with VITE_API_BASE_URL in .env.local (see .env.example).
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5015';

export const dailySummariesUrl = (symbol: string) =>
  `${API_BASE_URL}/api/stocks/${encodeURIComponent(symbol)}/daily-summaries`;
