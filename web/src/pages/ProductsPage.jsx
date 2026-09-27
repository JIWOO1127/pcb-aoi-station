import { useMemo, useState } from 'react';
import { Plus, Search } from 'lucide-react';
import { products as initialProducts } from '../mocks/data';
import { ConfirmModal, DataTable, Field, Modal, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

export default function ProductsPage() {
  const [items, setItems] = useState(initialProducts);
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState('');
  const [modal, setModal] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const filtered = useMemo(() => items.filter((item) => (!keyword || `${item.id} ${item.name}`.toLowerCase().includes(keyword.toLowerCase())) && (!status || item.status === status)), [items, keyword, status]);
  const columns = [
    { key: 'id', label: '제품 ID', render: (v) => <b className="cell-id">{v}</b> }, { key: 'name', label: '제품명' }, { key: 'description', label: '설명' },
    { key: 'recipe', label: 'Active Recipe', render: (v) => <span className="version-tag">{v}</span> }, { key: 'createdAt', label: '등록일' },
    { key: 'status', label: '상태', render: (v) => <StatusBadge value={v} dot /> },
    { key: 'actions', label: '관리', render: (_, row) => <div className="row-actions"><button onClick={(e) => { e.stopPropagation(); setModal(row); }}>수정</button><button onClick={(e) => { e.stopPropagation(); setConfirm(row); }}>{row.status === 'ACTIVE' ? '비활성' : '활성'}</button></div> },
  ];
  const save = () => { if (modal?.new) setItems((prev) => [{ id: 'PCB-G720', name: '신규 제품', description: '신규 등록 제품', recipe: '-', createdAt: '2026-09-27', status: 'ACTIVE' }, ...prev]); setModal(null); };
  return <><PageHeader eyebrow="MASTER DATA" title="제품 관리" description="AOI 검사 대상 제품과 활성 Recipe를 관리합니다." action={<button className="btn btn-primary" onClick={() => setModal({ new: true })}><Plus size={16} /> 제품 등록</button>} />
    <SearchFilter onReset={() => { setKeyword(''); setStatus(''); }} onSearch={() => {}}><Field label="제품 검색"><div className="input-icon small"><Search size={15} /><input value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="제품 ID 또는 제품명" /></div></Field><Field label="상태"><select value={status} onChange={(e) => setStatus(e.target.value)}><option value="">전체</option><option value="ACTIVE">활성</option><option value="INACTIVE">비활성</option></select></Field></SearchFilter>
    <div className="list-summary"><span>제품 목록</span><b>{filtered.length}</b><small>개 제품</small></div><div className="panel table-panel"><DataTable columns={columns} rows={filtered} pageSize={6} /></div>
    <Modal open={!!modal} title={modal?.new ? '제품 등록' : '제품 정보 수정'} onClose={() => setModal(null)} footer={<><button className="btn btn-ghost" onClick={() => setModal(null)}>취소</button><button className="btn btn-primary" onClick={save}>저장</button></>}><div className="form-grid"><Field label="제품 ID"><input defaultValue={modal?.id || ''} placeholder="예: PCB-G720" /></Field><Field label="제품명"><input defaultValue={modal?.name || ''} placeholder="제품명 입력" /></Field><Field label="설명"><textarea defaultValue={modal?.description || ''} placeholder="제품 설명" /></Field></div></Modal>
    <ConfirmModal open={!!confirm} title={`제품 ${confirm?.status === 'ACTIVE' ? '비활성화' : '활성화'}`} message={`${confirm?.id} 제품의 상태를 변경하시겠습니까? 과거 검사 이력은 그대로 보존됩니다.`} onClose={() => setConfirm(null)} onConfirm={() => { setItems((prev) => prev.map((item) => item.id === confirm.id ? { ...item, status: item.status === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE' } : item)); setConfirm(null); }} confirmText="상태 변경" />
  </>;
}
