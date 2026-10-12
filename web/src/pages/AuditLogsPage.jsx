import { useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { auditService } from '../services/api';
import { DataTable, Field, LoadingState, PageHeader, SearchFilter } from '../components/UI';

// 오늘 날짜를 YYYY-MM-DD 형식으로 반환
const getToday = () => {
  const now = new Date();

  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
};


const emptyFilters = {
  actorId: '',
  action: '',
  from: getToday(),
  to: getToday(),
};

export default function AuditLogsPage() {
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [filters, setFilters] = useState(emptyFilters);

  const load = async (nextFilters = filters) => {
    setLoading(true); setError('');
    try {
      setRows(await auditService.list({
        ...nextFilters,
        from: nextFilters.from ? `${nextFilters.from}T00:00:00+09:00` : '',
        to: nextFilters.to ? `${nextFilters.to}T23:59:59+09:00` : '',
        size: 100,
      }));
    } catch (e) { setError(e.message); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(emptyFilters); }, []);

  const resetFilters = () => { setFilters(emptyFilters); load(emptyFilters); };
  const exportCsv = () => {
    const escape = (value) => `"${String(value ?? '').replaceAll('"', '""')}"`;
    const records = rows.map((row) => [new Date(row.occurredAt).toLocaleString('ko-KR'), row.actorId, row.action, row.targetType, row.targetId, row.details]);
    const csv = [['Timestamp', '사용자', 'Action', '대상 유형', '대상', '변경 내용'], ...records].map((record) => record.map(escape).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url; link.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.csv`; link.click();
    URL.revokeObjectURL(url);
  };

  const columns = [
    { key: 'occurredAt', label: 'Timestamp', render: (value) => new Date(value).toLocaleString('ko-KR') },
    { key: 'actorId', label: '사용자' },
    { key: 'action', label: 'Action', render: (value) => <code className="audit-action">{value}</code> },
    { key: 'targetType', label: '대상 유형' },
    { key: 'targetId', label: '대상' },
    { key: 'details', label: '변경 내용' },
  ];

  return <>
    <PageHeader eyebrow="SYSTEM MANAGEMENT" title="감사 로그" description="관리자의 주요 변경 작업과 사용자 로그인을 추적합니다." action={<button className="btn btn-ghost" disabled={!rows.length} onClick={exportCsv}><Download size={15} /> CSV 내보내기</button>} />
    <SearchFilter onSearch={() => load()} onReset={resetFilters}>
      <Field label="기간"><div className="date-range"><input type="date" value={filters.from} onChange={(event) => setFilters({ ...filters, from: event.target.value })} /><span>—</span><input type="date" value={filters.to} onChange={(event) => setFilters({ ...filters, to: event.target.value })} /></div></Field>
      <Field label="사용자 사번"><input value={filters.actorId} onChange={(event) => setFilters({ ...filters, actorId: event.target.value })} placeholder="전체 사용자" /></Field>
      <Field label="Action"><select value={filters.action} onChange={(event) => setFilters({ ...filters, action: event.target.value })}><option value="">전체 Action</option><option>LOGIN</option><option>USER_CREATE</option><option>USER_UPDATE</option><option>USER_PASSWORD_RESET</option></select></Field>
    </SearchFilter>
    {error && <div className="info-banner"><b>API 오류</b>{error}</div>}
    {loading ? <LoadingState /> : <div className="panel table-panel"><DataTable columns={columns} rows={rows} pageSize={20} /></div>}
  </>;
}
