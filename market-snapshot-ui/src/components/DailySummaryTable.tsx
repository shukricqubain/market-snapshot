import type { DailySummary } from '../api/types';
export default function DailySummaryTable({ rows }: { rows: DailySummary[] }) {
  if (rows.length === 0) return <p>No data.</p>;

  return (
    <table>
      <thead>
        <tr>
          <th>Day</th>
          <th>Low average</th>
          <th>High average</th>
          <th>Volume</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr key={r.day}>
            <td>{r.day}</td>
            <td>{r.lowAverage.toFixed(4)}</td>
            <td>{r.highAverage.toFixed(4)}</td>
            <td>{r.volume.toLocaleString('en-US')}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}