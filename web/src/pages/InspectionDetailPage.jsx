import { useEffect, useState } from 'react';
import { ArrowLeft, Clock3, MapPin, Package, UserRound } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { inspectionService } from '../services/api';
import { DataTable, LoadingState, PageHeader, Panel, StatusBadge } from '../components/UI';

const formatDate = (value) => value ? new Date(value).toLocaleString('ko-KR') : '-';

export default function InspectionDetailPage() {
  const { inspectionId } = useParams();
  const navigate = useNavigate();
  const [item, setItem] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    inspectionService.get(inspectionId).then(setItem).catch((reason) => setError(reason.message));
  }, [inspectionId]);

  const columns = [
    { key: 'partId', label: '부품 ID', render: (value) => <b>{value}</b> },
    { key: 'expectedClass', label: '예상 클래스', render: (value) => <code className="class-code">{value}</code> },
    { key: 'detectedClass', label: '검출 클래스', render: (value) => value || '-' },
    { key: 'expectedHoles', label: '예상 핀홀', render: (value) => value?.join(', ') || '-' },
    { key: 'coveredHoles', label: '검출 핀홀', render: (value) => value?.join(', ') || '-' },
    { key: 'defectType', label: '불량 유형', render: (value) => value || '정상' },
  ];

  return <>
    <button className="back-button" onClick={() => navigate('/inspections')}><ArrowLeft size={16} /> 검사 기록</button>
    {error && <div className="info-banner"><b>검사 기록 조회 실패</b><span>{error}</span></div>}
    {!item ? <LoadingState /> : <>
      <PageHeader eyebrow="INSPECTION DETAIL" title={item.inspectionId} description="스테이션에서 전송한 구조화된 검사 결과입니다." action={<StatusBadge value={item.verdict} />} />
      <div className="inspection-layout">
        <Panel title="검사 정보" subtitle="판정 및 실행 환경">
          <div className="info-list">
            <div><span><Package size={16} /> 제품</span><b>{item.productId}</b></div>
            <div><span><MapPin size={16} /> 스테이션</span><b>{item.stationId || '-'}</b></div>
            <div><span><Clock3 size={16} /> 검사 시간</span><b>{formatDate(item.capturedAt)}</b></div>
            <div><span><UserRound size={16} /> 작업자</span><b>{item.workerId || '-'}</b></div>
            <div><span>레시피</span><b>v{item.recipeVersion}</b></div>
            <div><span>레시피 ID</span><b>{item.recipeId}</b></div>
          </div>
        </Panel>
        <Panel title="처리 결과" subtitle="검사 판정과 처리 시간">
          <div className="info-list">
            <div><span>판정</span><StatusBadge value={item.verdict} /></div>
            <div><span>불량 수</span><b>{item.defectCount}</b></div>
            <div><span>처리 시간</span><b>{item.latencyMs} ms</b></div>
            <div><span>오류 코드</span><b>{item.errorCode || '-'}</b></div>
          </div>
        </Panel>
      </div>
      <Panel title="부품별 검사 결과" subtitle={`총 ${item.parts.length}개 부품`}>
        <DataTable columns={columns} rows={item.parts.map((part) => ({ ...part, id: part.partId }))} pageSize={20} />
      </Panel>
    </>}
  </>;
}
