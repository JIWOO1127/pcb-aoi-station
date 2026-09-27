import { useNavigate } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, CircleGauge, ClipboardCheck, Factory, RefreshCw } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { defectDistribution, hourlyData, inspections } from '../mocks/data';
import { DataTable, PageHeader, Panel, StatCard, StatusBadge } from '../components/UI';

const colors = ['#ef4444', '#f59e0b', '#8b5cf6', '#0ea5e9'];

export default function DashboardPage() {
  const navigate = useNavigate();
  const recentFail = inspections.filter((item) => item.result === 'FAIL');
  const columns = [
    { key: 'time', label: '검사 시간' }, { key: 'product', label: '제품' }, { key: 'station', label: 'Station' },
    { key: 'defect', label: '불량 유형', render: (v) => <code className="defect-code">{v}</code> }, { key: 'recipe', label: 'Recipe' },
    { key: 'result', label: '결과', render: (v) => <StatusBadge value={v} /> },
  ];
  return <><PageHeader eyebrow="OVERVIEW / TODAY" title="생산 품질 Dashboard" description="2026년 9월 27일 · 화성 제1공장 실시간 현황" action={<button className="btn btn-ghost"><RefreshCw size={15} /> 새로고침</button>} />
    <div className="stat-grid five"><StatCard label="오늘 총 검사" value="1,434" unit="건" icon={ClipboardCheck} trend="↑ 8.2%" caption="전일 대비" /><StatCard label="PASS" value="1,388" unit="건" icon={CheckCircle2} tone="green" trend="96.8%" caption="합격률" /><StatCard label="FAIL" value="46" unit="건" icon={AlertTriangle} tone="red" trend="↑ 5건" caption="전일 대비" /><StatCard label="불량률" value="3.21" unit="%" icon={CircleGauge} tone="amber" trend="↓ 0.4%p" caption="7일 평균 대비" /><StatCard label="가동 Station" value="3" unit="/ 6" icon={Factory} tone="cyan" trend="5대 연결" caption="1대 오류" /></div>
    <div className="dashboard-charts"><Panel title="시간대별 검사량" subtitle="오늘 시간당 처리 건수"><ResponsiveContainer width="100%" height={230}><BarChart data={hourlyData}><CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" /><XAxis dataKey="time" tickFormatter={(v) => `${v}시`} axisLine={false} tickLine={false} /><YAxis axisLine={false} tickLine={false} /><Tooltip cursor={{ fill: '#f1f5f9' }} /><Bar isAnimationActive={false} dataKey="inspections" fill="#2563eb" radius={[4, 4, 0, 0]} barSize={22} /></BarChart></ResponsiveContainer></Panel>
      <Panel title="시간대별 불량률" subtitle="Station 전체 평균"><ResponsiveContainer width="100%" height={230}><LineChart data={hourlyData}><CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" /><XAxis dataKey="time" tickFormatter={(v) => `${v}시`} axisLine={false} tickLine={false} /><YAxis unit="%" axisLine={false} tickLine={false} /><Tooltip /><Line isAnimationActive={false} type="monotone" dataKey="defectRate" stroke="#f97316" strokeWidth={3} dot={{ r: 3, fill: '#fff' }} /></LineChart></ResponsiveContainer></Panel>
      <Panel title="불량 유형 비율" subtitle="오늘 FAIL 46건"><div className="pie-layout"><ResponsiveContainer width="58%" height={230}><PieChart><Pie isAnimationActive={false} data={defectDistribution} dataKey="value" innerRadius={55} outerRadius={83} paddingAngle={3}>{defectDistribution.map((_, i) => <Cell key={i} fill={colors[i]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer><div className="chart-legend">{defectDistribution.map((item, i) => <div key={item.key}><i style={{ background: colors[i] }} /><span>{item.name}</span><b>{item.value}%</b></div>)}</div></div></Panel></div>
    <Panel title="최근 FAIL 검사" subtitle="최근 발생한 불량 검사 5건" action={<button className="text-button" onClick={() => navigate('/inspections')}>전체 이력 보기 →</button>}><DataTable columns={columns} rows={recentFail} pageSize={5} onRowClick={(row) => navigate(`/inspections/${row.id}`)} /></Panel>
  </>;
}
