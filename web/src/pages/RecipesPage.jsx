import { useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { recipes as initialRecipes } from '../mocks/data';
import { ConfirmModal, DataTable, Field, Modal, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

export default function RecipesPage() {
  const [items, setItems] = useState(initialRecipes);
  const [product, setProduct] = useState('');
  const [status, setStatus] = useState('');
  const [open, setOpen] = useState(false);
  const [activate, setActivate] = useState(null);
  const navigate = useNavigate();
  const rows = useMemo(() => items.filter((item) => (!product || item.product === product) && (!status || item.status === status)), [items, product, status]);
  const columns = [
    { key: 'id', label: 'Recipe ID', render: (v) => <b className="cell-id">{v}</b> }, { key: 'product', label: '제품' }, { key: 'name', label: 'Recipe 명' },
    { key: 'version', label: 'Version', render: (v, row) => <span className={row.status === 'ACTIVE' ? 'version-active' : 'version-tag'}>{v}</span> },
    { key: 'status', label: '상태', render: (v) => <StatusBadge value={v} dot /> }, { key: 'createdBy', label: '작성자' }, { key: 'createdAt', label: '작성일시' },
    { key: 'action', label: '관리', render: (_, row) => row.status === 'DRAFT' ? <button className="mini-primary" onClick={(e) => { e.stopPropagation(); setActivate(row); }}>적용</button> : <span className="muted">—</span> },
  ];
  return <><PageHeader eyebrow="MASTER DATA" title="Recipe 관리" description="제품별 검사 기준과 버전 이력을 관리합니다." action={<button className="btn btn-primary" onClick={() => setOpen(true)}><Plus size={16} /> Recipe 생성</button>} />
    <div className="info-banner"><b>Recipe 적용 정책</b><span>Station은 작업 시작 시점에 ACTIVE Recipe를 가져갑니다. 작업 중인 Station에는 즉시 반영되지 않습니다.</span></div>
    <SearchFilter onReset={() => { setProduct(''); setStatus(''); }}><Field label="제품"><select value={product} onChange={(e) => setProduct(e.target.value)}><option value="">전체 제품</option><option>PCB-A100</option><option>PCB-B220</option><option>PCB-C310</option></select></Field><Field label="Recipe 상태"><select value={status} onChange={(e) => setStatus(e.target.value)}><option value="">전체 상태</option><option value="DRAFT">초안</option><option value="ACTIVE">활성</option><option value="INACTIVE">비활성</option></select></Field></SearchFilter>
    <div className="list-summary"><span>Recipe 목록</span><b>{rows.length}</b><small>개 버전</small></div><div className="panel table-panel"><DataTable columns={columns} rows={rows} onRowClick={(row) => navigate(`/recipes/${row.id}`)} /></div>
    <Modal open={open} title="새 Recipe 생성" onClose={() => setOpen(false)} footer={<><button className="btn btn-ghost" onClick={() => setOpen(false)}>취소</button><button className="btn btn-primary" onClick={() => setOpen(false)}>초안 생성</button></>}><div className="form-grid two"><Field label="대상 제품"><select><option>PCB-A100 · Main Control Board</option><option>PCB-B220 · Sensor Interface</option></select></Field><Field label="Recipe 명"><input placeholder="Recipe 이름" /></Field><Field label="기준 버전"><select><option>v3.2 복제</option><option>빈 Recipe</option></select></Field><Field label="신규 버전"><input defaultValue="v3.3" /></Field></div></Modal>
    <ConfirmModal open={!!activate} title="Recipe 적용" message={`${activate?.product}의 ${activate?.version} 버전을 ACTIVE로 적용합니다. 기존 ACTIVE 버전은 INACTIVE로 변경됩니다.`} confirmText="ACTIVE 적용" onClose={() => setActivate(null)} onConfirm={() => { setItems((prev) => prev.map((item) => item.product !== activate.product ? item : { ...item, status: item.id === activate.id ? 'ACTIVE' : item.status === 'ACTIVE' ? 'INACTIVE' : item.status })); setActivate(null); }} />
  </>;
}
