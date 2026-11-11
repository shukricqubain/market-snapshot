// Mirrors the API's DailySummary JSON (camelCase, day as "yyyy-MM-dd").
export interface DailySummary {
  day: string;
  lowAverage: number;
  highAverage: number;
  volume: number;
}

// RFC 7807 body the API returns for 400/404/502 (ASP.NET Core ProblemDetails).
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
}
