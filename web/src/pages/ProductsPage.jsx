import { useEffect, useMemo, useState } from 'react';
import { Plus, Search } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { productService } from '../services/api';
import { ConfirmModal, DataTable, Field, LoadingState, Modal, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

const emptyProduct = { id: '', name: '', description: '', active: true, new: true };

export default function ProductsPage() {
  const [items, setItems] = useState([]);
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState('');
  const [modal, setModal] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const { user } = useAuth();
  const canEdit = ['ADMIN', 'ENGINEER'].includes(user?.role);

  const load = async () => {
    setLoading(true); setError('');
    try { setItems(await productService.list()); }
    catch (reason) { setError(reason.message); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const filtered = useMemo(() => items.filter((item) =>
    (!keyword || `${item.id} ${item.name}`.toLowerCase().includes(keyword.toLowerCase()))
    && (!status || item.status === status)), [items, keyword, status]);
  const columns = [
    { key: 'id', label: '제품 ID', render: (value) => <b className="cell-id">{value}</b> },
    { key: 'name', label: '제품명' }, { key: 'description', label: '설명' },
    { key: 'recipe', label: 'Active Recipe', render: (value) => <span className="version-tag">{value}</span> },
    { key: 'createdAt', label: '등록일', render: (value) => value ? new Date(value).toLocaleDateString('ko-KR') : '-' },
    { key: 'status', label: '상태', render: (value) => <StatusBadge value={value} dot /> },
    ...(canEdit ? [{ key: 'actions', label: '관리', render: (_, row) => <div className="row-actions">
      <button onClick={(event) => { event.stopPropagation(); setModal({ ...row, active: row.status === 'ACTIVE', new: false }); }}>수정</button>
      <button onClick={(event) => { event.stopPropagation(); setConfirm(row); }}>{row.status === 'ACTIVE' ? '비활성' : '활성'}</button>
    </div> }] : []),
  ];

  const save = async () => {
    if (!modal.id.trim() || !modal.name.trim()) { setError('제품 ID와 제품명을 입력해 주세요.'); return; }
    setSaving(true); setError('');
    try {
      if (modal.new) await productService.create({ id: modal.id.trim(), name: modal.name.trim(), description: modal.description.trim() || null });
      else await productService.update(modal.id, { name: modal.name.trim(), description: modal.description.trim() || null, active: modal.active });
      setModal(null); await load();
    } catch (reason) { setError(reason.message); }
    finally { setSaving(false); }
  };

  const toggle = async () => {
    setSaving(true); setError('');
    try {
      await productService.update(confirm.id, { name: confirm.name, description: confirm.description, active: confirm.status !== 'ACTIVE' });
      setConfirm(null); await load();
    } catch (reason) { setError(reason.message); }
    finally { setSaving(false); }
  };

  return <>
    <PageHeader eyebrow="MASTER DATA" title="제품 관리" description="AOI 검사 대상 제품과 활성 Recipe를 관리합니다."
      action={canEdit && <button className="btn btn-primary" onClick={() => { setError(''); setModal({ ...emptyProduct }); }}><Plus size={16} /> 제품 등록</button>} />
    <SearchFilter onReset={() => { setKeyword(''); setStatus(''); }}><Field label="제품 검색"><div className="input-icon small"><Search size={15} /><input value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="제품 ID 또는 제품명" /></div></Field><Field label="상태"><select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">전체</option><option value="ACTIVE">활성</option><option value="INACTIVE">비활성</option></select></Field></SearchFilter>
    {error && !modal && <div className="info-banner"><b>확인 필요</b><span>{error}</span></div>}
    <div className="list-summary"><span>제품 목록</span><b>{filtered.length}</b><small>개 제품</small></div>
    <div className="panel table-panel">{loading ? <LoadingState /> : <DataTable columns={columns} rows={filtered} pageSize={8} />}</div>
    <Modal open={!!modal} title={modal?.new ? '제품 등록' : '제품 정보 수정'} onClose={() => !saving && setModal(null)} footer={<><button className="btn btn-ghost" disabled={saving} onClick={() => setModal(null)}>취소</button><button className="btn btn-primary" disabled={saving} onClick={save}>{saving ? '저장 중...' : '저장'}</button></>}>
      {error && <div className="info-banner"><b>저장 실패</b><span>{error}</span></div>}
      <div className="form-grid"><Field label="제품 ID"><input value={modal?.id || ''} disabled={!modal?.new} onChange={(event) => setModal({ ...modal, id: event.target.value })} placeholder="예: PCB-G720" /></Field><Field label="제품명"><input value={modal?.name || ''} onChange={(event) => setModal({ ...modal, name: event.target.value })} /></Field><Field label="설명"><textarea value={modal?.description || ''} onChange={(event) => setModal({ ...modal, description: event.target.value })} /></Field></div>
    </Modal>
    <ConfirmModal open={!!confirm} title={`제품 ${confirm?.status === 'ACTIVE' ? '비활성화' : '활성화'}`} message={`${confirm?.id} 제품의 상태를 변경하시겠습니까?`} onClose={() => !saving && setConfirm(null)} onConfirm={toggle} confirmText="상태 변경" />
  </>;
}
