import { useMemo, useState } from 'react';
import { Download } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { inspections } from '../mocks/data';
import { DataTable, Field, PageHeader, SearchFilter, StatusBadge } from '../components/UI';

export default function InspectionsPage() {
  const [result, setResult] = useState(''); const [product, setProduct] = useState(''); const [station, setStation] = useState('');
  const navigate = useNavigate();
  const rows = useMemo(() => inspections.filter((item) => (!result || item.result === result) && (!product || item.product === product) && (!station || item.station === station)), [result, product, station]);
  const columns = [{ key: 'id', label: 'Inspection ID', render: (v) => <b className="cell-id">{v}</b> }, { key: 'time', label: '검사 시간' }, { key: 'product', label: '제품' }, { key: 'station', label: 'Station' }, { key: 'recipe', label: 'Recipe' }, { key: 'model', label: 'AI Model' }, { key: 'result', label: 'Result', render: (v) => <StatusBadge value={v} /> }];
  return <><PageHeader eyebrow="INSPECTION" title="검사 이력" description="Station에서 판정한 전체 검사 결과를 조회합니다." action={<button className="btn btn-ghost"><Download size={15} /> CSV 내보내기</button>} />
    <SearchFilter onReset={() => { setResult(''); setProduct(''); setStation(''); }}><Field label="기간"><div className="date-range"><input type="date" defaultValue="2026-09-21" /><span>—</span><input type="date" defaultValue="2026-09-27" /></div></Field><Field label="제품"><select value={product} onChange={(e) => setProduct(e.target.value)}><option value="">전체 제품</option><option>PCB-A100</option><option>PCB-B220</option><option>PCB-C310</option></select></Field><Field label="Station"><select value={station} onChange={(e) => setStation(e.target.value)}><option value="">전체 Station</option><option>ST-01</option><option>ST-02</option><option>ST-03</option></select></Field><Field label="판정"><select value={result} onChange={(e) => setResult(e.target.value)}><option value="">전체</option><option>PASS</option><option>FAIL</option></select></Field></SearchFilter>
    <div className="list-summary"><span>검사 결과</span><b>{rows.length}</b><small>건</small><div className="summary-legend"><span><i className="green" /> PASS {rows.filter((r) => r.result === 'PASS').length}</span><span><i className="red" /> FAIL {rows.filter((r) => r.result === 'FAIL').length}</span></div></div><div className="panel table-panel"><DataTable columns={columns} rows={rows} onRowClick={(row) => navigate(`/inspections/${row.id}`)} /></div>
  </>;
}
