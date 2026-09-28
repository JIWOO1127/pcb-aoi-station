import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, CircleGauge, ClipboardCheck, Factory, RefreshCw } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { inspectionService } from '../services/api';
import useCurrentTime from '../hooks/useCurrentTime';
import { DataTable, EmptyState, LoadingState, PageHeader, Panel, StatCard, StatusBadge } from '../components/UI';

const colors = ['#ef4444', '#f59e0b', '#8b5cf6', '#0ea5e9', '#64748b'];
const defectLabels = {
  MISSING: '부품 누락',
  OFFSET: '위치 불량',
  WRONG_PART: '오부품',
  WIRE_COLOR_SWAP: '전선 색상',
  EXTRA: '추가 부품',
};
const dateKey = (value) => new Date(value).toLocaleDateString('sv-SE', { timeZone: 'Asia/Seoul' });
const hourOf = (value) => Number(new Date(value).toLocaleTimeString('en-US', {
  timeZone: 'Asia/Seoul', hour: '2-digit', hourCycle: 'h23',
}));
const formatTime = (value) => new Date(value).toLocaleTimeString('ko-KR', {
  timeZone: 'Asia/Seoul', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
});

export default function DashboardPage() {
  const navigate = useNavigate();
  const now = useCurrentTime();
  const [inspections, setInspections] = useState([]);
  const [failDetails, setFailDetails] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const all = await inspectionService.list();
      const todayKey = dateKey(new Date());
      const todayFails = all.filter((item) => item.verdict === 'FAIL' && dateKey(item.capturedAt) === todayKey);
      const details = await Promise.allSettled(todayFails.map((item) => inspectionService.get(item.inspectionId)));
      setInspections(all);
      setFailDetails(details.filter((result) => result.status === 'fulfilled').map((result) => result.value));
    } catch (reason) {
      setInspections([]);
      setFailDetails([]);
      setError(reason.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const todayKey = dateKey(now);
  const currentHour = hourOf(now);
  const today = useMemo(() => {
    return inspections.filter((item) => dateKey(item.capturedAt) === todayKey);
  }, [inspections, todayKey]);

  const passCount = today.filter((item) => item.verdict === 'PASS').length;
  const failCount = today.filter((item) => item.verdict === 'FAIL').length;
  const invalidCount = today.filter((item) => item.verdict === 'INVALID').length;
  const passRate = today.length ? (passCount / today.length * 100).toFixed(1) : '0.0';
  const defectRate = today.length ? (failCount / today.length * 100).toFixed(2) : '0.00';
  const activeStations = new Set(today.map((item) => item.stationId).filter(Boolean)).size;

  const hourly = useMemo(() => {
    const startHour = Math.max(0, Math.min(8, currentHour));
    return Array.from({ length: currentHour - startHour + 1 }, (_, index) => {
      const hour = startHour + index;
      const bucket = today.filter((item) => hourOf(item.capturedAt) === hour);
      const fails = bucket.filter((item) => item.verdict === 'FAIL').length;
      return {
        time: String(hour).padStart(2, '0'),
        inspections: bucket.length,
        defectRate: bucket.length ? Number((fails / bucket.length * 100).toFixed(2)) : 0,
      };
    });
  }, [today, currentHour]);

  const defects = useMemo(() => {
    const counts = new Map();
    failDetails.flatMap((item) => item.parts).forEach((part) => {
      if (part.defectType) counts.set(part.defectType, (counts.get(part.defectType) || 0) + 1);
    });
    return [...counts.entries()].map(([key, value]) => ({ key, name: defectLabels[key] || key, value }));
  }, [failDetails]);

  const recentFail = today.filter((item) => item.verdict === 'FAIL').slice(0, 5).map((item) => ({
    ...item,
    id: item.inspectionId,
    time: formatTime(item.capturedAt),
    product: item.productId,
    station: item.stationId || '-',
    recipe: `v${item.recipeVersion}`,
    result: item.verdict,
  }));
  const columns = [
    { key: 'time', label: '검사 시간' }, { key: 'product', label: '제품' }, { key: 'station', label: '스테이션' },
    { key: 'recipe', label: '레시피' }, { key: 'defectCount', label: '불량 수' },
    { key: 'result', label: '결과', render: (value) => <StatusBadge value={value} /> },
  ];
  const currentLabel = now.toLocaleString('ko-KR', {
    timeZone: 'Asia/Seoul', year: 'numeric', month: 'long', day: 'numeric', weekday: 'short',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
  });

  return <>
    <PageHeader eyebrow="OVERVIEW / TODAY" title="생산 품질 Dashboard" description={`${currentLabel} · 실시간 현황`} action={<button className="btn btn-ghost" onClick={load} disabled={loading}><RefreshCw size={15} /> 새로고침</button>} />
    {error && <div className="info-banner"><b>대시보드 조회 실패</b><span>{error}</span></div>}
    {loading ? <LoadingState /> : <>
      <div className="stat-grid five">
        <StatCard label="오늘 총 검사" value={today.length.toLocaleString()} unit="건" icon={ClipboardCheck} caption="실시간 집계" />
        <StatCard label="PASS" value={passCount.toLocaleString()} unit="건" icon={CheckCircle2} tone="green" trend={`${passRate}%`} caption="합격률" />
        <StatCard label="FAIL" value={failCount.toLocaleString()} unit="건" icon={AlertTriangle} tone="red" caption={invalidCount ? `판정 불가 ${invalidCount}건` : '실시간 집계'} />
        <StatCard label="불량률" value={defectRate} unit="%" icon={CircleGauge} tone="amber" caption="오늘 기준" />
        <StatCard label="검사 Station" value={activeStations} unit="대" icon={Factory} tone="cyan" caption="오늘 기록 기준" />
      </div>
      <div className="dashboard-charts">
        <Panel title="시간대별 검사량" subtitle="현재 시각까지 시간당 처리 건수">
          {today.length === 0 ? <div className="chart-empty"><EmptyState text="오늘 저장된 검사 기록이 없습니다." /></div> : <ResponsiveContainer width="100%" height={230}><BarChart data={hourly}><CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" /><XAxis dataKey="time" tickFormatter={(value) => `${value}시`} axisLine={false} tickLine={false} /><YAxis allowDecimals={false} axisLine={false} tickLine={false} /><Tooltip cursor={{ fill: '#f1f5f9' }} /><Bar isAnimationActive={false} dataKey="inspections" name="검사 수" fill="#2563eb" radius={[4, 4, 0, 0]} barSize={22} /></BarChart></ResponsiveContainer>}
        </Panel>
        <Panel title="시간대별 불량률" subtitle="현재 시각까지 스테이션 전체 평균">
          {today.length === 0 ? <div className="chart-empty"><EmptyState text="불량률을 계산할 검사 기록이 없습니다." /></div> : <ResponsiveContainer width="100%" height={230}><LineChart data={hourly}><CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e8edf4" /><XAxis dataKey="time" tickFormatter={(value) => `${value}시`} axisLine={false} tickLine={false} /><YAxis unit="%" axisLine={false} tickLine={false} /><Tooltip /><Line isAnimationActive={false} type="monotone" dataKey="defectRate" name="불량률" stroke="#f97316" strokeWidth={3} dot={{ r: 3, fill: '#fff' }} /></LineChart></ResponsiveContainer>}
        </Panel>
        <Panel title="불량 유형" subtitle={`오늘 FAIL ${failCount}건`}>
          {defects.length === 0 ? <div className="chart-empty"><EmptyState text="집계할 불량 기록이 없습니다." /></div> : <div className="pie-layout"><ResponsiveContainer width="58%" height={230}><PieChart><Pie isAnimationActive={false} data={defects} dataKey="value" innerRadius={55} outerRadius={83} paddingAngle={3}>{defects.map((item, index) => <Cell key={item.key} fill={colors[index % colors.length]} />)}</Pie><Tooltip /></PieChart></ResponsiveContainer><div className="chart-legend">{defects.map((item, index) => <div key={item.key}><i style={{ background: colors[index % colors.length] }} /><span>{item.name}</span><b>{item.value}건</b></div>)}</div></div>}
        </Panel>
      </div>
      <Panel title="최근 FAIL 검사" subtitle="오늘 발생한 최근 불량 검사 5건" action={<button className="text-button" onClick={() => navigate('/inspections')}>전체 이력 보기 →</button>}>
        <DataTable columns={columns} rows={recentFail} pageSize={5} emptyText="오늘 발생한 FAIL 검사가 없습니다." onRowClick={(row) => navigate(`/inspections/${row.inspectionId}`)} />
      </Panel>
    </>}
  </>;
}
