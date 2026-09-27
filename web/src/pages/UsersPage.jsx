import { useEffect, useMemo, useState } from 'react';
import { KeyRound, Plus, Search } from 'lucide-react';
import { userService } from '../services/api';
import { DataTable, Field, LoadingState, Modal, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

const emptyForm = { employeeId: '', name: '', password: '', role: 'VIEWER', active: true };

export default function UsersPage() {
  const [items, setItems] = useState([]); const [keyword, setKeyword] = useState('');
  const [modal, setModal] = useState(null); const [reset, setReset] = useState(null);
  const [loading, setLoading] = useState(true); const [error, setError] = useState('');
  const load = async () => { setLoading(true); setError(''); try { setItems(await userService.list()); } catch (e) { setError(e.message); } finally { setLoading(false); } };
  useEffect(() => { load(); }, []);
  const rows = useMemo(() => items.filter((x) => !keyword || `${x.employeeId} ${x.name}`.includes(keyword)), [items, keyword]);
  const save = async () => { try { if (modal.isNew) await userService.create(modal); else await userService.update(modal.employeeId, modal); setModal(null); await load(); } catch (e) { setError(e.message); } };
  const resetPassword = async () => { try { await userService.resetPassword(reset.employeeId, reset.password); setReset(null); await load(); } catch (e) { setError(e.message); } };
  const columns = [{ key: 'employeeId', label: '사번', render: (v) => <b className="cell-id">{v}</b> }, { key: 'name', label: '이름' }, { key: 'role', label: 'Role', render: (v) => <StatusBadge value={v} /> }, { key: 'active', label: '상태', render: (v) => <StatusBadge value={v ? 'ACTIVE' : 'INACTIVE'} dot /> }, { key: 'action', label: '관리', render: (_, row) => <div className="row-actions"><button onClick={() => setModal({ ...row, password: '', isNew: false })}>수정</button><button onClick={() => setReset({ ...row, password: '' })}><KeyRound size={13} /> 초기화</button></div> }];
  return <><PageHeader eyebrow="SYSTEM MANAGEMENT" title="사용자 관리" description="MES 접근 계정과 역할 권한을 관리합니다. ADMIN 전용 메뉴입니다." action={<button className="btn btn-primary" onClick={() => setModal({ ...emptyForm, isNew: true })}><Plus size={16} /> 사용자 추가</button>} />
    <SearchFilter onReset={() => setKeyword('')}><Field label="사용자 검색"><div className="input-icon small"><Search size={15} /><input value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="사번 또는 이름" /></div></Field></SearchFilter>
    {error && <div className="info-banner"><b>API 오류</b>{error}</div>}{loading ? <LoadingState /> : <div className="panel table-panel"><DataTable columns={columns} rows={rows} /></div>}
    <Modal open={!!modal} title={modal?.isNew ? '사용자 추가' : '사용자 정보 수정'} onClose={() => setModal(null)} footer={<><button className="btn btn-ghost" onClick={() => setModal(null)}>취소</button><button className="btn btn-primary" onClick={save}>저장</button></>}><div className="form-grid two"><Field label="사번"><input value={modal?.employeeId || ''} disabled={!modal?.isNew} onChange={(e) => setModal({ ...modal, employeeId: e.target.value })} /></Field><Field label="이름"><input value={modal?.name || ''} onChange={(e) => setModal({ ...modal, name: e.target.value })} /></Field>{modal?.isNew && <Field label="초기 PIN"><input type="password" value={modal?.password || ''} onChange={(e) => setModal({ ...modal, password: e.target.value })} /></Field>}<Field label="Role"><select value={modal?.role || 'VIEWER'} onChange={(e) => setModal({ ...modal, role: e.target.value })}><option>ADMIN</option><option>ENGINEER</option><option>VIEWER</option></select></Field><Field label="상태"><select value={modal?.active ? 'ACTIVE' : 'INACTIVE'} onChange={(e) => setModal({ ...modal, active: e.target.value === 'ACTIVE' })}><option>ACTIVE</option><option>INACTIVE</option></select></Field></div></Modal>
    <Modal open={!!reset} title="비밀번호 초기화" onClose={() => setReset(null)} footer={<><button className="btn btn-ghost" onClick={() => setReset(null)}>취소</button><button className="btn btn-primary" onClick={resetPassword}>초기화</button></>}><Field label={`${reset?.name || ''} 새 PIN`}><input type="password" value={reset?.password || ''} onChange={(e) => setReset({ ...reset, password: e.target.value })} /></Field></Modal>
  </>;
}
