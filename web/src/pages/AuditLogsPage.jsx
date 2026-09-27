import { useEffect, useState } from 'react';
import { auditService } from '../services/api';
import { DataTable, Field, LoadingState, PageHeader, SearchFilter } from '../components/UI';

export default function AuditLogsPage() {
  const [rows, setRows] = useState([]); const [loading, setLoading] = useState(true);
  const [error, setError] = useState(''); const [filters, setFilters] = useState({ actorId: '', action: '', from: '', to: '' });
  const load = async () => { setLoading(true); setError(''); try { setRows(await auditService.list({ ...filters, from: filters.from ? `${filters.from}T00:00:00+09:00` : '', to: filters.to ? `${filters.to}T23:59:59+09:00` : '' })); } catch (e) { setError(e.message); } finally { setLoading(false); } };
  useEffect(() => { load(); }, []);
  const columns = [{ key: 'occurredAt', label: 'Timestamp', render: (v) => new Date(v).toLocaleString('ko-KR') }, { key: 'actorId', label: '사용자' }, { key: 'action', label: 'Action', render: (v) => <code className="audit-action">{v}</code> }, { key: 'targetType', label: '대상 유형' }, { key: 'targetId', label: '대상' }, { key: 'details', label: '변경 내용' }];
  return <><PageHeader eyebrow="SYSTEM MANAGEMENT" title="감사 로그" description="관리자와 엔지니어의 주요 변경 작업을 추적합니다." />
    <SearchFilter onSearch={load} onReset={() => setFilters({ actorId: '', action: '', from: '', to: '' })}><Field label="기간"><div className="date-range"><input type="date" value={filters.from} onChange={(e) => setFilters({ ...filters, from: e.target.value })} /><span>—</span><input type="date" value={filters.to} onChange={(e) => setFilters({ ...filters, to: e.target.value })} /></div></Field><Field label="사용자 사번"><input value={filters.actorId} onChange={(e) => setFilters({ ...filters, actorId: e.target.value })} placeholder="전체 사용자" /></Field><Field label="Action"><select value={filters.action} onChange={(e) => setFilters({ ...filters, action: e.target.value })}><option value="">전체 Action</option><option>LOGIN</option><option>USER_CREATE</option><option>USER_UPDATE</option><option>USER_PASSWORD_RESET</option></select></Field></SearchFilter>
    {error && <div className="info-banner"><b>API 오류</b>{error}</div>}{loading ? <LoadingState /> : <div className="panel table-panel"><DataTable columns={columns} rows={rows} /></div>}
  </>;
}
