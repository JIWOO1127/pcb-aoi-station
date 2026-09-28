import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { recipeService } from '../services/api';
import { DataTable, Field, LoadingState, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

const formatDate = (value) => value ? new Date(value).toLocaleString('ko-KR') : '-';

export default function RecipesPage() {
  const [items, setItems] = useState([]);
  const [product, setProduct] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    recipeService.list()
      .then(setItems)
      .catch((reason) => setError(reason.message))
      .finally(() => setLoading(false));
  }, []);

  const rows = useMemo(() => items
    .map((item) => ({
      ...item,
      id: item.recipeId,
      product: item.productId,
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

  return <>
    <PageHeader eyebrow="MASTER DATA" title="레시피 관리" description="제품별 검사 레시피와 부품 기준을 조회합니다." />
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
    {error && <div className="info-banner"><b>레시피 조회 실패</b><span>{error}</span></div>}
    <div className="list-summary"><span>레시피 목록</span><b>{rows.length}</b><small>개 버전</small></div>
    <div className="panel table-panel">
      {loading ? <LoadingState /> : <DataTable columns={columns} rows={rows} onRowClick={(row) => navigate(`/recipes/${row.recipeId}`)} />}
    </div>
  </>;
}
