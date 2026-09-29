import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, CircleGauge, ClipboardCheck, Factory, RefreshCw } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { dashboardService } from '../services/api';
import { DataTable, LoadingState, PageHeader, Panel, StatCard, StatusBadge } from '../components/UI';

const colors = ['#ef4444', '#f59e0b', '#8b5cf6', '#0ea5e9', '#64748b'];

export default function DashboardPage() {
  const navigate = useNavigate();
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const load = useCallback(async () => {
    try {
      setData(await dashboardService.getSummary());
      setError('');
    } catch (reason) {
      setError(reason.message);
    }
  }, []);

  useEffect(() => {
    load();
    const timer = window.setInterval(load, 5000);
    const refreshWhenVisible = () => { if (!document.hidden) load(); };
    document.addEventListener('visibilitychange', refreshWhenVisible);
    window.addEventListener('focus', load);

    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', refreshWhenVisible);
      window.removeEventListener('focus', load);
    };
  }, [load]);
  const today = new Date().toLocaleDateString('ko-KR', { year: 'numeric', month: 'long', day: 'numeric' });
  const columns = [
    { key: 'time', label: '검사 시간' }, { key: 'product', label: '제품' }, { key: 'station', label: 'Station' },
    { key: 'defect', label: '불량 유형', render: (value) => <code className="defect-code">{value}</code> },
    { key: 'recipe', label: 'Recipe' }, { key: 'result', label: '결과', render: (value) => <StatusBadge value={value} /> },
  ];
  if (!data && !error) return <LoadingState />;
  const stats = data?.stats || { total: 0, pass: 0, fail: 0, defectRate: 0, runningStations: 0, connectedStations: 0, totalStations: 0, errorStations: 0 };
  const hourly = data?.hourly || [];
  const defects = data?.defects || [];
  const recent = data?.recent || [];
  return <><PageHeader eyebrow="OVERVIEW / TODAY" title="생산 품질 Dashboard" description={`${today} · 화성 제1공장 실시간 현황`} action={<button className="btn btn-ghost" onClick={load}><RefreshCw size={15} /> 새로고침</button>} />
    {error && <div className="info-banner"><b>조회 실패</b><span>{error}</span></div>}
    <div className="stat-grid five"><StatCard label="오늘 총 검사" value={stats.total.toLocaleString()} unit="건" icon={ClipboardCheck} caption="DB 실시간 집계" /><StatCard label="PASS" value={stats.pass.toLocaleString()} unit="건" icon={CheckCircle2} tone="green" trend={`${stats.total ? ((stats.pass / stats.total) * 100).toFixed(1) : '0.0'}%`} caption="합격률" /><StatCard label="FAIL" value={stats.fail.toLocaleString()} unit="건" icon={AlertTriangle} tone="red" caption="오늘 누적" /><StatCard label="불량률" value={stats.defectRate.toFixed(2)} unit="%" icon={CircleGauge} tone="amber" caption="오늘 기준" /><StatCard label="가동 Station" value={stats.runningStations} unit={`/ ${stats.totalStations}`} icon={Factory} tone="cyan" trend={`${stats.connectedStations}대 연결`} caption={`${stats.errorStations}대 오류`} /></div>
    <div className="dashboard-charts"><Panel title="시간대별 검사량" subtitle="오늘 시간당 처리 건수"><ResponsiveContainer width="100%" height={230}><BarChart data={hourly}><CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" /><XAxis dataKey="time" tickFormatter={(value) => `${value}시`} axisLine={false} tickLine={false} /><YAxis axisLine={false} tickLine={false} /><Tooltip /><Bar isAnimationActive={false} dataKey="inspections" fill="#2563eb" radius={[4,4,0,0]} barSize={22} /></BarChart></ResponsiveContainer></Panel>
      <Panel title="시간대별 불량률" subtitle="Station 전체 평균"><ResponsiveContainer width="100%" height={230}><LineChart data={hourly}><CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" /><XAxis dataKey="time" tickFormatter={(value) => `${value}시`} axisLine={false} tickLine={false} /><YAxis unit="%" axisLine={false} tickLine={false} /><Tooltip /><Line isAnimationActive={false} type="monotone" dataKey="defectRate" stroke="#f97316" strokeWidth={3} dot={{ r: 3, fill: '#fff' }} /></LineChart></ResponsiveContainer></Panel>
      <Panel title="불량 유형 비율" subtitle={`오늘 FAIL ${stats.fail}건`}><div className="pie-layout"><ResponsiveContainer width="58%" height={230}><PieChart><Pie isAnimationActive={false} data={defects} dataKey="value" innerRadius={55} outerRadius={83} paddingAngle={3}>{defects.map((item, index) => <Cell key={item.key} fill={colors[index % colors.length]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer><div className="chart-legend">{defects.map((item, index) => <div key={item.key}><i style={{ background: colors[index % colors.length] }} /><span>{item.name}</span><b>{item.value}%</b></div>)}</div></div></Panel></div>
    <Panel title="최근 FAIL 검사" subtitle="최근 발생한 불량 검사 5건" action={<button className="text-button" onClick={() => navigate('/inspections')}>전체 이력 보기 →</button>}><DataTable columns={columns} rows={recent} pageSize={5} onRowClick={(row) => navigate(`/inspections/${row.id}`)} /></Panel>
  </>;
}
