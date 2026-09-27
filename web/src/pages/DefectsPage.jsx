import { Bar, BarChart, CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { dailyDefects, defectDistribution, productDefects } from '../mocks/data';
import { Field, PageHeader, Panel, SearchFilter, StatCard } from '../components/UI';
import { AlertTriangle, CircleGauge, TrendingDown } from 'lucide-react';

const colors = ['#ef4444', '#f59e0b', '#8b5cf6', '#0ea5e9'];
const stationData = [{ name: 'ST-01', rate: 2.8 }, { name: 'ST-02', rate: 2.1 }, { name: 'ST-03', rate: 5.9 }, { name: 'ST-04', rate: 3.4 }, { name: 'ST-05', rate: 2.6 }];
const ChartGrid = () => <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" />;

export default function DefectsPage() {
  return <><PageHeader eyebrow="QUALITY ANALYTICS" title="불량 분석" description="저장된 검사 결과를 기준으로 품질 지표를 분석합니다." />
    <SearchFilter><Field label="기간"><div className="date-range"><input type="date" defaultValue="2026-09-21" /><span>—</span><input type="date" defaultValue="2026-09-27" /></div></Field><Field label="제품"><select><option>전체 제품</option><option>PCB-A100</option></select></Field><Field label="Station"><select><option>전체 Station</option><option>ST-01</option></select></Field><Field label="AI Model"><select><option>전체 모델</option><option>RTMDet-m v2.4</option></select></Field></SearchFilter>
    <div className="stat-grid three compact"><StatCard label="기간 내 FAIL" value="284" unit="건" icon={AlertTriangle} tone="red" trend="↓ 12건" caption="이전 기간 대비" /><StatCard label="평균 불량률" value="3.14" unit="%" icon={CircleGauge} tone="amber" trend="↓ 0.31%p" caption="이전 기간 대비" /><StatCard label="품질 개선" value="9.0" unit="%" icon={TrendingDown} tone="green" trend="양호" caption="7일 추세" /></div>
    <div className="analytics-grid"><Panel title="불량 유형별 비율" subtitle="Defect type distribution"><div className="pie-layout"><ResponsiveContainer width="60%" height={260}><PieChart><Pie isAnimationActive={false} data={defectDistribution} dataKey="value" innerRadius={62} outerRadius={94} paddingAngle={3}>{defectDistribution.map((_, i) => <Cell key={i} fill={colors[i]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer><div className="chart-legend">{defectDistribution.map((x, i) => <div key={x.key}><i style={{ background: colors[i] }} /><span>{x.name}</span><b>{x.value}%</b></div>)}</div></div></Panel>
      <Panel title="일별 불량률 추이" subtitle="최근 7일"><ResponsiveContainer width="100%" height={260}><LineChart data={dailyDefects}><ChartGrid /><XAxis dataKey="date" axisLine={false} tickLine={false} /><YAxis unit="%" axisLine={false} tickLine={false} /><Tooltip /><Line isAnimationActive={false} type="monotone" dataKey="rate" stroke="#2563eb" strokeWidth={3} dot={{ r: 4, fill: '#fff' }} /></LineChart></ResponsiveContainer></Panel>
      <Panel title="제품별 불량률" subtitle="제품군 품질 비교"><ResponsiveContainer width="100%" height={250}><BarChart data={productDefects} layout="vertical"><ChartGrid /><XAxis type="number" unit="%" axisLine={false} tickLine={false} /><YAxis dataKey="name" type="category" width={85} axisLine={false} tickLine={false} /><Tooltip /><Bar isAnimationActive={false} dataKey="rate" fill="#2563eb" radius={[0,4,4,0]} barSize={16} /></BarChart></ResponsiveContainer></Panel>
      <Panel title="Station별 불량률" subtitle="설비 품질 비교"><ResponsiveContainer width="100%" height={250}><BarChart data={stationData}><ChartGrid /><XAxis dataKey="name" axisLine={false} tickLine={false} /><YAxis unit="%" axisLine={false} tickLine={false} /><Tooltip /><Bar isAnimationActive={false} dataKey="rate" radius={[4,4,0,0]} barSize={26}>{stationData.map((x) => <Cell key={x.name} fill={x.rate > 5 ? '#ef4444' : '#0f766e'} />)}</Bar></BarChart></ResponsiveContainer></Panel></div>
  </>;
}
