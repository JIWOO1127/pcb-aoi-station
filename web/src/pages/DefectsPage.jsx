import { useEffect, useState } from 'react';
import { Bar, BarChart, CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { AlertTriangle, CircleGauge, TrendingDown } from 'lucide-react';
import { defectService } from '../services/api';
import { Field, LoadingState, PageHeader, Panel, SearchFilter, StatCard } from '../components/UI';

const colors = ['#ef4444', '#f59e0b', '#8b5cf6', '#0ea5e9', '#64748b'];
const dateValue = (date) => date.toISOString().slice(0, 10);
const initialFilters = { from: dateValue(new Date(Date.now() - 6 * 86400000)), to: dateValue(new Date()), productId: '', stationId: '', modelId: '' };
const ChartGrid = () => <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" />;

export default function DefectsPage() {
  const [filters, setFilters] = useState(initialFilters);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const load = async (values = filters) => { setLoading(true); try { setData(await defectService.getAnalytics(values)); setError(''); } catch (reason) { setError(reason.message); } finally { setLoading(false); } };
  useEffect(() => { load(initialFilters); }, []);
  const reset = () => { setFilters(initialFilters); load(initialFilters); };
  const update = (key, value) => setFilters((current) => ({ ...current, [key]: value }));

  return <><PageHeader eyebrow="QUALITY ANALYTICS" title="불량 분석" description="저장된 검사 결과를 기준으로 품질 지표를 분석합니다." />
    <SearchFilter onReset={reset} onSearch={() => load()}><Field label="기간"><div className="date-range"><input type="date" value={filters.from} onChange={(event) => update('from', event.target.value)} /><span>—</span><input type="date" value={filters.to} onChange={(event) => update('to', event.target.value)} /></div></Field><Field label="제품"><select value={filters.productId} onChange={(event) => update('productId', event.target.value)}><option value="">전체 제품</option>{data?.products.map((value) => <option key={value}>{value}</option>)}</select></Field><Field label="Station"><select value={filters.stationId} onChange={(event) => update('stationId', event.target.value)}><option value="">전체 Station</option>{data?.stations.map((value) => <option key={value}>{value}</option>)}</select></Field><Field label="AI Model"><select value={filters.modelId} onChange={(event) => update('modelId', event.target.value)}><option value="">전체 모델</option>{data?.models.map((value) => <option key={value}>{value}</option>)}</select></Field></SearchFilter>
    {error && <div className="info-banner"><b>조회 실패</b><span>{error}</span></div>}
    {loading && !data ? <LoadingState /> : <>
      <div className="stat-grid three compact"><StatCard label="기간 내 FAIL" value={data?.failCount || 0} unit="건" icon={AlertTriangle} tone="red" caption="선택 기간" /><StatCard label="평균 불량률" value={(data?.defectRate || 0).toFixed(2)} unit="%" icon={CircleGauge} tone="amber" caption="전체 검사 대비" /><StatCard label="품질 개선" value={(data?.qualityImprovement || 0).toFixed(1)} unit="%" icon={TrendingDown} tone="green" caption="기간 시작 대비" /></div>
      <div className="analytics-grid"><Panel title="불량 유형별 비율" subtitle="Defect type distribution"><div className="pie-layout"><ResponsiveContainer width="60%" height={260}><PieChart><Pie isAnimationActive={false} data={data?.defectDistribution || []} dataKey="value" innerRadius={62} outerRadius={94} paddingAngle={3}>{data?.defectDistribution.map((item, index) => <Cell key={item.key} fill={colors[index % colors.length]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer><div className="chart-legend">{data?.defectDistribution.map((item, index) => <div key={item.key}><i style={{ background: colors[index % colors.length] }} /><span>{item.name}</span><b>{item.value}%</b></div>)}</div></div></Panel>
        <Panel title="일별 불량률 추이" subtitle="선택 기간"><ResponsiveContainer width="100%" height={260}><LineChart data={data?.dailyDefects || []}><ChartGrid /><XAxis dataKey="date" axisLine={false} tickLine={false} /><YAxis unit="%" axisLine={false} tickLine={false} /><Tooltip /><Line isAnimationActive={false} type="monotone" dataKey="rate" stroke="#2563eb" strokeWidth={3} dot={{ r: 4, fill: '#fff' }} /></LineChart></ResponsiveContainer></Panel>
        <Panel title="제품별 불량률" subtitle="제품군 품질 비교"><ResponsiveContainer width="100%" height={250}><BarChart data={data?.productDefects || []} layout="vertical"><ChartGrid /><XAxis type="number" unit="%" axisLine={false} tickLine={false} /><YAxis dataKey="name" type="category" width={85} axisLine={false} tickLine={false} /><Tooltip /><Bar isAnimationActive={false} dataKey="rate" fill="#2563eb" radius={[0,4,4,0]} barSize={16} /></BarChart></ResponsiveContainer></Panel>
        <Panel title="Station별 불량률" subtitle="설비 품질 비교"><ResponsiveContainer width="100%" height={250}><BarChart data={data?.stationDefects || []}><ChartGrid /><XAxis dataKey="name" axisLine={false} tickLine={false} /><YAxis unit="%" axisLine={false} tickLine={false} /><Tooltip /><Bar isAnimationActive={false} dataKey="rate" radius={[4,4,0,0]} barSize={26}>{data?.stationDefects.map((item) => <Cell key={item.name} fill={item.rate > 5 ? '#ef4444' : '#0f766e'} />)}</Bar></BarChart></ResponsiveContainer></Panel></div>
    </>}
  </>;
}
