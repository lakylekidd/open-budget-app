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

// Monotone cubic (Fritsch-Carlson) interpolation as SVG cubic Bezier commands: smooth, never overshoots the data.
export function smoothPath(pts: { x: number; y: number }[]) {
  const n = pts.length;
  if (!n) return '';
  const f = (v: number) => v.toFixed(1);
  let d = `M${f(pts[0].x)},${f(pts[0].y)}`;
  if (n === 1) return d;
  const dx = pts.slice(1).map((p, i) => p.x - pts[i].x), slope = pts.slice(1).map((p, i) => dx[i] ? (p.y - pts[i].y) / dx[i] : 0);
  const m = Array.from({ length: n }, (_, i) => i === 0 ? slope[0] : i === n - 1 ? slope[n - 2] : slope[i - 1] * slope[i] <= 0 ? 0 : (slope[i - 1] + slope[i]) / 2);
  for (let i = 0; i < n - 1; i++) {
    if (slope[i] === 0) { m[i] = 0; m[i + 1] = 0; continue; }
    const a = m[i] / slope[i], b = m[i + 1] / slope[i], h = Math.hypot(a, b);
    if (h > 3) { m[i] = 3 * a / h * slope[i]; m[i + 1] = 3 * b / h * slope[i]; }
  }
  for (let i = 0; i < n - 1; i++) {
    const w = dx[i] / 3;
    d += ` C${f(pts[i].x + w)},${f(pts[i].y + m[i] * w)} ${f(pts[i + 1].x - w)},${f(pts[i + 1].y - m[i + 1] * w)} ${f(pts[i + 1].x)},${f(pts[i + 1].y)}`;
  }
  return d;
}

// Loan balance over time: recorded month-end debt (solid, with a shaded area) and the projected track (dashed).
// Both series are the same debt, so they share the accent colour and differ by line style; a legend names them.
export function LoanChart({ past, track, simulation, label = 'Loan balance chart' }: { past: BalancePoint[]; track: BalancePoint[]; simulation?: BalancePoint[] | null; label?: string }) {
  const id = useId();
  const [hover, setHover] = useState<number | null>(null);
  const sim = simulation ?? [];
  const months = [...past.map(p => p.month), ...track.map(p => p.month), ...sim.map(p => p.month)];
  if (!months.length) return null;
  const first = months.reduce((a, b) => a < b ? a : b), last = months.reduce((a, b) => a > b ? a : b);
  const index = (month: string) => (Number(month.slice(0, 4)) - Number(first.slice(0, 4))) * 12 + Number(month.slice(5, 7)) - Number(first.slice(5, 7));
  const span = Math.max(index(last), 1);
  const maxValue = Math.max(...past.map(p => p.balance), ...track.map(p => p.balance), ...sim.map(p => p.balance), 1);
  const step = niceStep(maxValue);
  const top_ = Math.ceil(maxValue / step) * step;
  const x = (month: string) => left + (index(month) / span) * (width - left - right);
  const y = (value: number) => top + (1 - value / top_) * (height - top - bottom);
  const path = (points: BalancePoint[]) => smoothPath(points.map(p => ({ x: x(p.month), y: y(p.balance) })));
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
    <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-labelledby={`${id}-title`}>
      <title id={`${id}-title`}>{label}</title>
      <desc>{`Balance from ${shortMonthLabel(first)} to ${shortMonthLabel(last)}, peaking at ${tick(maxValue)}.${sim.length > 1 ? ' Includes a simulated payoff track.' : ''}`}</desc>
      {ticks.map(value => <g key={value}><line className="loan-chart-grid" x1={left} x2={width - right} y1={y(value)} y2={y(value)} /><text className="loan-chart-axis" x={left - 8} y={y(value) + 4} textAnchor="end">{tick(value)}</text></g>)}
      {years.map(year => <text key={year} className="loan-chart-axis" x={x(`${year}-01` < first ? first : `${year}-01`)} y={height - 8} textAnchor="middle">{year}</text>)}
      {area && <path className="loan-chart-area" d={area} />}
      {past.length > 0 && <path className="loan-chart-past" d={path(past)} />}
      {track.length > 1 && <path className="loan-chart-track" d={path(track)} />}
      {sim.length > 1 && <path className="loan-chart-sim" d={path(sim)} />}
      {track.length > 0 && <circle className="loan-chart-now" cx={x(track[0].month)} cy={y(track[0].balance)} r={5} />}
      {active && <g className="loan-chart-hover"><line x1={x(active.month)} x2={x(active.month)} y1={top} y2={y(0)} /><circle cx={x(active.month)} cy={y(active.balance)} r={4} /></g>}
      <rect className="loan-chart-hit" x={left} y={top} width={width - left - right} height={height - top - bottom} onPointerMove={move} onPointerLeave={() => setHover(null)} />
    </svg>
    <div className="loan-chart-legend"><span><i className="solid" />Past Balance</span>
      <span><i className="dashed" />Current Track<b className="loan-info" title="Your balance if you keep paying the required minimum every month." aria-label="About Current Track">ⓘ</b></span>
      {simulation !== undefined && <span><i className="dashed sim" />Simulation<b className="loan-info" title="Your balance with the payment, payoff date or extra payment you entered." aria-label="About Simulation">ⓘ</b></span>}</div>
    {active && <div className="loan-chart-tooltip" role="status" style={{ left: `${(x(active.month) / width) * 100}%` }}><strong>{shortMonthLabel(active.month)}</strong><span>{active.kind}: {formatMoney(active.balance)}</span></div>}
  </figure>;
}
