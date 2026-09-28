import { useEffect, useMemo, useState } from 'react';
import { KeyRound, Plus, Search } from 'lucide-react';
import { userService } from '../services/api';
import { DataTable, Field, LoadingState, Modal, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

const emptyForm = { employeeId: '', name: '', password: '', role: 'VIEWER', active: true };
const formatDateTime = (value) => value ? new Date(value).toLocaleString('ko-KR') : '로그인 이력 없음';

export default function UsersPage() {
  const [items, setItems] = useState([]);
  const [keyword, setKeyword] = useState('');
  const [role, setRole] = useState('');
  const [active, setActive] = useState('');
  const [modal, setModal] = useState(null);
  const [reset, setReset] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try { setItems(await userService.list()); }
    catch (e) { setError(e.message); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const rows = useMemo(() => items.filter((item) => {
    const matchesKeyword = !keyword || `${item.employeeId} ${item.name}`.toLowerCase().includes(keyword.toLowerCase());
    const matchesRole = !role || item.role === role;
    const matchesActive = !active || String(item.active) === active;
    return matchesKeyword && matchesRole && matchesActive;
  }), [items, keyword, role, active]);

  const save = async () => {
    if (!modal?.employeeId.trim() || !modal?.name.trim() || (modal.isNew && modal.password.length < 4)) {
      setError('사번과 이름을 입력하고, 초기 PIN은 4자 이상으로 설정해 주세요.');
      return;
    }
    setSaving(true); setError(''); setNotice('');
    try {
      if (modal.isNew) await userService.create(modal);
      else await userService.update(modal.employeeId, { name: modal.name, role: modal.role, active: modal.active });
      setNotice(modal.isNew ? '사용자를 추가했습니다.' : '사용자 정보를 수정했습니다.');
      setModal(null);
      await load();
    } catch (e) { setError(e.message); }
    finally { setSaving(false); }
  };

  const resetPassword = async () => {
    if (!reset?.password || reset.password.length < 4) {
      setError('새 PIN은 4자 이상으로 설정해 주세요.');
      return;
    }
    setSaving(true); setError(''); setNotice('');
    try {
      await userService.resetPassword(reset.employeeId, reset.password);
      setNotice(`${reset.name} 사용자의 PIN을 초기화했습니다.`);
      setReset(null);
    } catch (e) { setError(e.message); }
    finally { setSaving(false); }
  };

  const resetFilters = () => { setKeyword(''); setRole(''); setActive(''); };
  const columns = [
    { key: 'employeeId', label: '사번', render: (value) => <b className="cell-id">{value}</b> },
    { key: 'name', label: '이름' },
    { key: 'role', label: 'Role', render: (value) => <StatusBadge value={value} /> },
    { key: 'active', label: '상태', render: (value) => <StatusBadge value={value ? 'ACTIVE' : 'INACTIVE'} dot /> },
    { key: 'lastLoginAt', label: '최근 로그인', render: formatDateTime },
    { key: 'action', label: '관리', render: (_, row) => <div className="row-actions"><button onClick={() => setModal({ ...row, password: '', isNew: false })}>수정</button><button onClick={() => setReset({ ...row, password: '' })}><KeyRound size={13} /> PIN 초기화</button></div> },
  ];

  return <>
    <PageHeader eyebrow="SYSTEM MANAGEMENT" title="사용자 관리" description="MES 접근 계정과 역할 권한을 관리합니다. ADMIN 전용 메뉴입니다." action={<button className="btn btn-primary" onClick={() => { setError(''); setNotice(''); setModal({ ...emptyForm, isNew: true }); }}><Plus size={16} /> 사용자 추가</button>} />
    <SearchFilter onReset={resetFilters}>
      <Field label="사용자 검색"><div className="input-icon small"><Search size={15} /><input value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="사번 또는 이름" /></div></Field>
      <Field label="Role"><select value={role} onChange={(event) => setRole(event.target.value)}><option value="">전체 Role</option><option>ADMIN</option><option>ENGINEER</option><option>VIEWER</option></select></Field>
      <Field label="상태"><select value={active} onChange={(event) => setActive(event.target.value)}><option value="">전체 상태</option><option value="true">ACTIVE</option><option value="false">INACTIVE</option></select></Field>
    </SearchFilter>
    {error && <div className="info-banner"><b>오류</b>{error}</div>}
    {notice && <div className="info-banner"><b>완료</b>{notice}</div>}
    {loading ? <LoadingState /> : <div className="panel table-panel"><DataTable columns={columns} rows={rows} /></div>}
    <Modal open={!!modal} title={modal?.isNew ? '사용자 추가' : '사용자 정보 수정'} onClose={() => !saving && setModal(null)} footer={<><button className="btn btn-ghost" disabled={saving} onClick={() => setModal(null)}>취소</button><button className="btn btn-primary" disabled={saving} onClick={save}>{saving ? '저장 중...' : '저장'}</button></>}>
      <div className="form-grid two">
        <Field label="사번"><input required minLength={3} maxLength={50} value={modal?.employeeId || ''} disabled={!modal?.isNew} onChange={(event) => setModal({ ...modal, employeeId: event.target.value })} /></Field>
        <Field label="이름"><input required maxLength={100} value={modal?.name || ''} onChange={(event) => setModal({ ...modal, name: event.target.value })} /></Field>
        {modal?.isNew && <Field label="초기 PIN"><input required type="password" minLength={4} maxLength={64} value={modal?.password || ''} onChange={(event) => setModal({ ...modal, password: event.target.value })} /></Field>}
        <Field label="Role"><select value={modal?.role || 'VIEWER'} onChange={(event) => setModal({ ...modal, role: event.target.value })}><option>ADMIN</option><option>ENGINEER</option><option>VIEWER</option></select></Field>
        <Field label="상태"><select value={modal?.active ? 'ACTIVE' : 'INACTIVE'} onChange={(event) => setModal({ ...modal, active: event.target.value === 'ACTIVE' })}><option>ACTIVE</option><option>INACTIVE</option></select></Field>
      </div>
    </Modal>
    <Modal open={!!reset} title="PIN 초기화" onClose={() => !saving && setReset(null)} footer={<><button className="btn btn-ghost" disabled={saving} onClick={() => setReset(null)}>취소</button><button className="btn btn-primary" disabled={saving} onClick={resetPassword}>{saving ? '처리 중...' : '초기화'}</button></>}>
      <Field label={`${reset?.name || ''} 새 PIN`}><input required type="password" minLength={4} maxLength={64} value={reset?.password || ''} onChange={(event) => setReset({ ...reset, password: event.target.value })} /></Field>
    </Modal>
  </>;
}
