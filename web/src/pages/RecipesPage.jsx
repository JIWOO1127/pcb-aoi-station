import { useEffect, useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { recipeService } from '../services/api';
import { DataTable, Field, LoadingState, Modal, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

const formatDate = (value) => value ? new Date(value).toLocaleString('ko-KR') : '-';
const emptyPart = () => ({ partId: '', expectedClass: '', holes: '' });

export default function RecipesPage() {
  const [items, setItems] = useState([]);
  const [product, setProduct] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [form, setForm] = useState(null);
  const navigate = useNavigate();
  const { user } = useAuth();
  const canCreate = ['ADMIN', 'ENGINEER'].includes(user?.role);

  const load = async () => {
    setLoading(true);
    setError('');
    try { setItems(await recipeService.list()); }
    catch (reason) { setError(reason.message); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const rows = useMemo(() => items
    .map((item) => ({
      ...item,
      id: item.recipeId,
      status: item.active ? 'ACTIVE' : 'INACTIVE',
      createdAtLabel: formatDate(item.createdAt),
    }))
    .filter((item) => (!product || item.productId === product)
      && (!status || item.status === status)), [items, product, status]);

  const products = [...new Set(items.map((item) => item.productId))];
  const columns = [
    { key: 'recipeId', label: '레시피 ID', render: (value) => <b className="cell-id">{value}</b> },
    { key: 'productId', label: '제품' },
    { key: 'version', label: '버전', render: (value, row) => <span className={row.active ? 'version-active' : 'version-tag'}>v{value}</span> },
    { key: 'note', label: '비고', render: (value) => value || '-' },
    { key: 'status', label: '상태', render: (value) => <StatusBadge value={value} dot /> },
    { key: 'createdAtLabel', label: '생성 일시' },
  ];

  const openCreate = () => {
    const productId = product || products[0] || '';
    const versions = items.filter((item) => item.productId === productId).map((item) => item.version);
    setError('');
    setForm({ productId, version: Math.max(0, ...versions) + 1, note: '', parts: [emptyPart()] });
  };

  const updatePart = (index, key, value) => setForm((current) => ({
    ...current,
    parts: current.parts.map((part, partIndex) => partIndex === index ? { ...part, [key]: value } : part),
  }));

  const save = async () => {
    const productId = form.productId.trim();
    const parts = form.parts.map((part) => ({
      partId: part.partId.trim(),
      expectedClass: part.expectedClass.trim(),
      holes: part.holes.split(',').map((hole) => hole.trim().toUpperCase()).filter(Boolean),
    }));
    if (!productId || !Number.isInteger(Number(form.version)) || Number(form.version) < 1) {
      setError('제품 ID와 1 이상의 버전을 입력해 주세요.');
      return;
    }
    if (!parts.length || parts.some((part) => !part.partId || !part.expectedClass || !part.holes.length)) {
      setError('각 부품의 ID, 예상 클래스, 홀을 모두 입력해 주세요.');
      return;
    }

    setSaving(true);
    setError('');
    try {
      const created = await recipeService.create({
        productId,
        version: Number(form.version),
        note: form.note.trim() || null,
        parts,
      });
      setForm(null);
      navigate(`/recipes/${created.recipeId}`);
    } catch (reason) { setError(reason.message); }
    finally { setSaving(false); }
  };

  return <>
    <PageHeader
      eyebrow="MASTER DATA"
      title="레시피 관리"
      description="제품별 검사 레시피와 부품 기준을 조회합니다."
      action={canCreate && <button className="btn btn-primary" onClick={openCreate}><Plus size={16} /> 새 레시피</button>}
    />
    <SearchFilter onReset={() => { setProduct(''); setStatus(''); }}>
      <Field label="제품">
        <select value={product} onChange={(event) => setProduct(event.target.value)}>
          <option value="">전체 제품</option>
          {products.map((value) => <option key={value}>{value}</option>)}
        </select>
      </Field>
      <Field label="상태">
        <select value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">전체 상태</option>
          <option value="ACTIVE">활성</option>
          <option value="INACTIVE">비활성</option>
        </select>
      </Field>
    </SearchFilter>
    {error && <div className="info-banner"><b>확인 필요</b><span>{error}</span></div>}
    <div className="list-summary"><span>레시피 목록</span><b>{rows.length}</b><small>개 버전</small></div>
    <div className="panel table-panel">
      {loading ? <LoadingState /> : <DataTable columns={columns} rows={rows} emptyText={items.length ? '검색 조건에 맞는 레시피가 없습니다.' : '등록된 레시피가 없습니다.'} onRowClick={(row) => navigate(`/recipes/${row.recipeId}`)} />}
    </div>

    <Modal open={!!form} title="새 레시피 만들기" onClose={() => !saving && setForm(null)} footer={<>
      <button className="btn btn-ghost" disabled={saving} onClick={() => setForm(null)}>취소</button>
      <button className="btn btn-primary" disabled={saving} onClick={save}>{saving ? '저장 중...' : '레시피 저장'}</button>
    </>}>
      {error && <div className="info-banner"><b>저장 실패</b><span>{error}</span></div>}
      <div className="form-grid two recipe-form">
        <Field label="제품 ID"><input maxLength={50} value={form?.productId || ''} onChange={(event) => setForm({ ...form, productId: event.target.value })} placeholder="NE555-B" /></Field>
        <Field label="버전"><input type="number" min="1" value={form?.version || 1} onChange={(event) => setForm({ ...form, version: event.target.value })} /></Field>
        <Field label="비고"><textarea maxLength={2000} value={form?.note || ''} onChange={(event) => setForm({ ...form, note: event.target.value })} placeholder="변경 사유나 참고 사항" /></Field>
      </div>
      <div className="recipe-parts-head"><div><b>부품 기준</b><span>홀은 쉼표로 구분해 주세요.</span></div><button className="btn btn-ghost" onClick={() => setForm({ ...form, parts: [...form.parts, emptyPart()] })}><Plus size={14} /> 부품 추가</button></div>
      <div className="recipe-parts-editor">
        {form?.parts.map((part, index) => <div className="recipe-part-row" key={index}>
          <input aria-label="부품 ID" maxLength={50} value={part.partId} onChange={(event) => updatePart(index, 'partId', event.target.value)} placeholder="R1" />
          <input aria-label="예상 클래스" maxLength={100} value={part.expectedClass} onChange={(event) => updatePart(index, 'expectedClass', event.target.value)} placeholder="resistance330" />
          <input aria-label="홀 목록" value={part.holes} onChange={(event) => updatePart(index, 'holes', event.target.value)} placeholder="E9, H9" />
          <button className="icon-btn" aria-label="부품 삭제" disabled={form.parts.length === 1} onClick={() => setForm({ ...form, parts: form.parts.filter((_, partIndex) => partIndex !== index) })}><Trash2 size={16} /></button>
        </div>)}
      </div>
    </Modal>
  </>;
}
