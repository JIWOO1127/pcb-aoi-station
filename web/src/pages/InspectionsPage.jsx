import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { inspectionService } from '../services/api';
import { DataTable, Field, LoadingState, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

const formatDate = (value) => value ? new Date(value).toLocaleString('ko-KR') : '-';

export default function InspectionsPage() {
  const [items, setItems] = useState([]);
  const [result, setResult] = useState('');
  const [product, setProduct] = useState('');
  const [station, setStation] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const load = useCallback(async (initial = false) => {
    if (initial) setLoading(true);
    try {
      setItems(await inspectionService.list());
      setError('');
    } catch (reason) {
      setError(reason.message);
    } finally {
      if (initial) setLoading(false);
    }
  }, []);

  useEffect(() => {
    load(true);
    const timer = window.setInterval(() => load(), 5000);
    const refreshWhenVisible = () => { if (!document.hidden) load(); };
    const refreshOnFocus = () => load();
    document.addEventListener('visibilitychange', refreshWhenVisible);
    window.addEventListener('focus', refreshOnFocus);

    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', refreshWhenVisible);
      window.removeEventListener('focus', refreshOnFocus);
    };
  }, [load]);

  const rows = useMemo(() => items
    .map((item) => ({
      ...item,
      id: item.inspectionId,
      time: formatDate(item.capturedAt),
      recipe: `v${item.recipeVersion}`,
      result: item.verdict,
    }))
    .filter((item) => (!result || item.verdict === result)
      && (!product || item.productId === product)
      && (!station || item.stationId === station)), [items, result, product, station]);

  const products = [...new Set(items.map((item) => item.productId))];
  const stations = [...new Set(items.map((item) => item.stationId).filter(Boolean))];
  const columns = [
    { key: 'inspectionId', label: '검사 ID', render: (value) => <b className="cell-id">{value}</b> },
    { key: 'time', label: '검사 시간' },
    { key: 'productId', label: '제품' },
    { key: 'stationId', label: '스테이션' },
    { key: 'recipe', label: '레시피' },
    { key: 'latencyMs', label: '처리 시간', render: (value) => `${value} ms` },
    { key: 'result', label: '결과', render: (value) => <StatusBadge value={value} /> },
  ];

  return <>
    <PageHeader eyebrow="INSPECTION" title="검사 기록" description="스테이션에서 전송한 검사 결과를 조회합니다." />
    <SearchFilter onReset={() => { setResult(''); setProduct(''); setStation(''); }}>
      <Field label="제품"><select value={product} onChange={(event) => setProduct(event.target.value)}><option value="">전체 제품</option>{products.map((value) => <option key={value}>{value}</option>)}</select></Field>
      <Field label="스테이션"><select value={station} onChange={(event) => setStation(event.target.value)}><option value="">전체 스테이션</option>{stations.map((value) => <option key={value}>{value}</option>)}</select></Field>
      <Field label="판정"><select value={result} onChange={(event) => setResult(event.target.value)}><option value="">전체</option><option>PASS</option><option>FAIL</option><option>INVALID</option></select></Field>
    </SearchFilter>
    {error && <div className="info-banner"><b>검사 기록 조회 실패</b><span>{error}</span></div>}
    <div className="list-summary">
      <span>검사 결과</span><b>{rows.length}</b><small>건</small>
      <div className="summary-legend"><span><i className="green" /> PASS {rows.filter((row) => row.result === 'PASS').length}</span><span><i className="red" /> FAIL {rows.filter((row) => row.result === 'FAIL').length}</span></div>
    </div>
    <div className="panel table-panel">
      {loading ? <LoadingState /> : <DataTable columns={columns} rows={rows} onRowClick={(row) => navigate(`/inspections/${row.inspectionId}`)} />}
    </div>
  </>;
}
