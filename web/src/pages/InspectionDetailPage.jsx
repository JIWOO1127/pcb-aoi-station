import { ArrowLeft, Clock3, Cpu, MapPin, Package } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { inspections } from '../mocks/data';
import { DataTable, PageHeader, Panel, PcbBoard, StatusBadge } from '../components/UI';

const detections = [
  { id: 1, className: 'ic_chip', confidence: '99.2%', bbox: '[182, 96, 112, 84]' }, { id: 2, className: 'ic_chip', confidence: '98.8%', bbox: '[418, 102, 110, 82]' },
  { id: 3, className: 'led', confidence: '98.1%', bbox: '[356, 104, 42, 38]' }, { id: 4, className: 'capacitor', confidence: '97.4%', bbox: '[214, 226, 54, 72]' },
  { id: 5, className: 'resistance330', confidence: '96.9%', bbox: '[402, 218, 84, 32]' }, { id: 6, className: 'red_wire', confidence: '95.7%', bbox: '[92, 284, 126, 28]' },
];

export default function InspectionDetailPage() {
  const { inspectionId } = useParams(); const navigate = useNavigate();
  const item = inspections.find((row) => row.id === inspectionId) || inspections[0];
  const columns = [{ key: 'className', label: 'Class', render: (v) => <code className="class-code">{v}</code> }, { key: 'confidence', label: 'Confidence' }, { key: 'bbox', label: 'Bounding Box' }];
  return <><button className="back-button" onClick={() => navigate('/inspections')}><ArrowLeft size={16} /> 검사 이력</button><PageHeader eyebrow="INSPECTION DETAIL" title={item.id} description="Station에서 전송된 검사 결과 원본입니다." action={<StatusBadge value={item.result} />} />
    <div className="inspection-layout"><Panel title="검사 이미지" subtitle="Object Detection 결과"><PcbBoard defect={item.result === 'FAIL'} /><div className="board-legend"><span><i className="green" /> 정상 검출</span><span><i className="red" /> 불량 후보</span><span>1280 × 960</span></div></Panel><Panel title="검사 정보" subtitle="판정 및 실행 환경"><div className="info-list"><div><span><Package size={16} /> Product</span><b>{item.product}</b></div><div><span><MapPin size={16} /> Station</span><b>{item.station}</b></div><div><span><Clock3 size={16} /> Inspection Time</span><b>{item.time}</b></div><div><span>Recipe Version</span><b>{item.recipe}</b></div><div><span><Cpu size={16} /> AI Model</span><b>{item.model}</b></div><div><span>Result</span><StatusBadge value={item.result} /></div></div></Panel></div>
    {item.result === 'FAIL' && <div className="defect-detail"><div className="defect-title"><span>!</span><div><p>DEFECT DETAIL</p><h3>{item.defect}</h3></div></div><div><span>Expected</span><b>LED count = 2</b></div><div><span>Detected</span><b className="danger-text">LED count = 1</b></div><div><span>판정 주체</span><b>C# Station Server</b></div></div>}
    <Panel title="Detection 결과" subtitle={`총 ${detections.length}개 객체 검출`}><DataTable columns={columns} rows={detections} pageSize={8} /></Panel>
  </>;
}
