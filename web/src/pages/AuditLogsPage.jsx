import { useMemo, useState } from 'react';
import { Download } from 'lucide-react';
import { auditLogs } from '../mocks/data';
import { DataTable, Field, PageHeader, SearchFilter } from '../components/UI';

export default function AuditLogsPage() {
  const [action, setAction] = useState('');
  const rows = useMemo(() => auditLogs.filter((x) => !action || x.action === action), [action]);
  const columns = [{ key: 'time', label: 'Timestamp' }, { key: 'user', label: 'User' }, { key: 'action', label: 'Action', render: (v) => <code className="audit-action">{v}</code> }, { key: 'target', label: 'Target' }, { key: 'before', label: 'Before', render: (v) => <span className="change-before">{v}</span> }, { key: 'after', label: 'After', render: (v) => <span className="change-after">{v}</span> }];
  return <><PageHeader eyebrow="SYSTEM MANAGEMENT" title="감사 로그" description="관리자와 엔지니어의 주요 변경 작업을 추적합니다." action={<button className="btn btn-ghost"><Download size={15} /> 로그 내보내기</button>} /><SearchFilter onReset={() => setAction('')}><Field label="기간"><div className="date-range"><input type="date" defaultValue="2026-09-21" /><span>—</span><input type="date" defaultValue="2026-09-27" /></div></Field><Field label="사용자"><select><option>전체 사용자</option><option>김민준</option><option>이서연</option></select></Field><Field label="Action"><select value={action} onChange={(e) => setAction(e.target.value)}><option value="">전체 Action</option><option>LOGIN</option><option>PRODUCT_UPDATE</option><option>RECIPE_UPDATE</option><option>RECIPE_ACTIVATE</option><option>MODEL_ACTIVATE</option><option>USER_UPDATE</option></select></Field></SearchFilter><div className="panel table-panel"><DataTable columns={columns} rows={rows} /></div></>;
}
