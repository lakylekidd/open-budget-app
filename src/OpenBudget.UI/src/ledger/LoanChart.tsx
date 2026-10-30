import { useId, useState } from 'react';
import { formatMoney } from './api';
import { type BalancePoint, shortMonthLabel } from './loans';

const width = 720, height = 240, left = 58, right = 12, top = 14, bottom = 28;

// Compact euro tick labels: €375K, €1.2M, €0.
const tick = (value: number) => value >= 1_000_000 ? `€${Number((value / 1_000_000).toFixed(1))}M` : value >= 1000 ? `€${Number((value / 1000).toFixed(0))}K` : `€${Math.round(value)}`;

function niceStep(max: number) {
  const rough = max / 5;
  const power = 10 ** Math.floor(Math.log10(rough || 1));
  return ([1, 1.5, 2, 2.5, 5, 7.5, 10].find(step => step * power >= rough) ?? 10) * power;
}

// Loan balance over time: recorded month-end debt (solid, with a shaded area) and the projected track (dashed).
// Both series are the same debt, so they share the accent colour and differ by line style; a legend names them.
export function LoanChart({ past, track, label = 'Loan balance chart' }: { past: BalancePoint[]; track: BalancePoint[]; label?: string }) {
  const id = useId();
  const [hover, setHover] = useState<number | null>(null);
  const months = [...past.map(p => p.month), ...track.map(p => p.month)];
  if (!months.length) return null;
  const first = months.reduce((a, b) => a < b ? a : b), last = months.reduce((a, b) => a > b ? a : b);
  const index = (month: string) => (Number(month.slice(0, 4)) - Number(first.slice(0, 4))) * 12 + Number(month.slice(5, 7)) - Number(first.slice(5, 7));
  const span = Math.max(index(last), 1);
  const maxValue = Math.max(...past.map(p => p.balance), ...track.map(p => p.balance), 1);
  const step = niceStep(maxValue);
  const top_ = Math.ceil(maxValue / step) * step;
  const x = (month: string) => left + (index(month) / span) * (width - left - right);
  const y = (value: number) => top + (1 - value / top_) * (height - top - bottom);
  const path = (points: BalancePoint[]) => points.map((p, i) => `${i ? 'L' : 'M'}${x(p.month).toFixed(1)},${y(p.balance).toFixed(1)}`).join(' ');
  const ticks = Array.from({ length: Math.round(top_ / step) + 1 }, (_, i) => i * step);
  const firstYear = Number(first.slice(0, 4)), lastYear = Number(last.slice(0, 4));
  const yearStep = Math.max(1, Math.ceil((lastYear - firstYear + 1) / 8));
  const years = Array.from({ length: lastYear - firstYear + 1 }, (_, i) => firstYear + i).filter(year => (year - firstYear) % yearStep === 0);
  const points = [...past.map(p => ({ ...p, kind: 'Past Balance' })), ...track.slice(past.length ? 1 : 0).map(p => ({ ...p, kind: 'Current Track' }))];
  const active = hover === null ? null : points[hover];
  const area = past.length ? `${path(past)} L${x(past[past.length - 1].month).toFixed(1)},${y(0)} L${x(past[0].month).toFixed(1)},${y(0)} Z` : '';
  function move(event: React.PointerEvent<SVGRectElement>) {
    const box = event.currentTarget.getBoundingClientRect();
    const position = ((event.clientX - box.left) / box.width) * (width - left - right);
    const target = (position / (width - left - right)) * span;
    let best = 0;
    points.forEach((p, i) => { if (Math.abs(index(p.month) - target) < Math.abs(index(points[best].month) - target)) best = i; });
    setHover(best);
  }
  return <figure className="loan-chart">
    <div className="loan-chart-legend"><span><i className="solid" />Past Balance</span><span><i className="dashed" />Current Track</span></div>
    <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-labelledby={`${id}-title`}>
      <title id={`${id}-title`}>{label}</title>
      {ticks.map(value => <g key={value}><line className="loan-chart-grid" x1={left} x2={width - right} y1={y(value)} y2={y(value)} /><text className="loan-chart-axis" x={left - 8} y={y(value) + 4} textAnchor="end">{tick(value)}</text></g>)}
      {years.map(year => <text key={year} className="loan-chart-axis" x={x(`${year}-01` < first ? first : `${year}-01`)} y={height - 8} textAnchor="middle">{year}</text>)}
      {area && <path className="loan-chart-area" d={area} />}
      {past.length > 0 && <path className="loan-chart-past" d={path(past)} />}
      {track.length > 1 && <path className="loan-chart-track" d={path(track)} />}
      {track.length > 0 && <circle className="loan-chart-now" cx={x(track[0].month)} cy={y(track[0].balance)} r={5} />}
      {active && <g className="loan-chart-hover"><line x1={x(active.month)} x2={x(active.month)} y1={top} y2={y(0)} /><circle cx={x(active.month)} cy={y(active.balance)} r={4} /></g>}
      <rect className="loan-chart-hit" x={left} y={top} width={width - left - right} height={height - top - bottom} onPointerMove={move} onPointerLeave={() => setHover(null)} />
    </svg>
    {active && <div className="loan-chart-tooltip" role="status" style={{ left: `${(x(active.month) / width) * 100}%` }}><strong>{shortMonthLabel(active.month)}</strong><span>{active.kind}: {formatMoney(active.balance)}</span></div>}
  </figure>;
}
