import { useState } from 'react';
import { Camera, Cpu, Database, Radio, RefreshCw } from 'lucide-react';
import { stations } from '../mocks/data';
import { Modal, PageHeader, StatusBadge } from '../components/UI';

export default function StationsPage() {
  const [selected, setSelected] = useState(null);
  return <><PageHeader eyebrow="EQUIPMENT MONITORING" title="Station 현황" description="생산 라인 AOI Station의 연결 및 가동 상태입니다." action={<button className="btn btn-ghost"><RefreshCw size={15} /> 자동 갱신 10s</button>} />
    <div className="station-summary"><span><i className="running" /> 가동 중 <b>3</b></span><span><i className="stop" /> 정지 <b>1</b></span><span><i className="error" /> 오류 <b>1</b></span><span><i className="offline" /> 오프라인 <b>1</b></span><small>마지막 갱신 15:48:32</small></div>
    <div className="station-grid">{stations.map((station) => <button className={`station-card station-${station.status.toLowerCase()}`} onClick={() => setSelected(station)} key={station.id}><div className="station-card-head"><div><span className="station-icon"><Radio size={20} /></span><div><h3>{station.id}</h3><p>{station.name}</p></div></div><StatusBadge value={station.status} dot /></div><div className="station-product"><span>현재 작업</span><b>{station.product}</b><small>Recipe {station.recipe}</small></div><div className="device-status"><div><Camera size={15} /><span>Camera</span><StatusBadge value={station.camera} dot /></div><div><Cpu size={15} /><span>AI Server</span><StatusBadge value={station.ai} dot /></div><div><Database size={15} /><span>MES</span><StatusBadge value={station.mes} dot /></div></div><div className="station-footer"><span>오늘 검사 <b>{station.today.toLocaleString()}건</b></span><span>Last seen {station.lastSeen}</span></div></button>)}</div>
    <Modal open={!!selected} title={`${selected?.id} Station 상세`} onClose={() => setSelected(null)} footer={<button className="btn btn-primary" onClick={() => setSelected(null)}>확인</button>}><div className="detail-summary vertical"><div><span>라인 명</span><b>{selected?.name}</b></div><div><span>상태</span>{selected && <StatusBadge value={selected.status} />}</div><div><span>현재 제품 / Recipe</span><b>{selected?.product} / {selected?.recipe}</b></div><div><span>오늘 검사</span><b>{selected?.today.toLocaleString()}건</b></div><div><span>마지막 통신</span><b>{selected?.lastSeen}</b></div></div></Modal>
  </>;
}
