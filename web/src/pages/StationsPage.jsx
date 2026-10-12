import { useEffect, useState } from 'react';
import { Camera, Cpu, Database, Radio, RefreshCw } from 'lucide-react';
import { stationService } from '../services/api';
import { LoadingState, Modal, PageHeader, StatusBadge } from '../components/UI';

const lastSeenLabel = (value) => value ? new Date(value).toLocaleString('ko-KR') : '-';

export default function StationsPage() {
  const [stations, setStations] = useState([]);
  const [selected, setSelected] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [updatedAt, setUpdatedAt] = useState(null);
  const load = async () => {
    try { setStations(await stationService.list()); setUpdatedAt(new Date()); setError(''); }
    catch (reason) { setError(reason.message); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); const timer = setInterval(load, 10000); return () => clearInterval(timer); }, []);
  const count = (status) => stations.filter((station) => station.status === status).length;

  return <><PageHeader eyebrow="EQUIPMENT MONITORING" title="Station 현황" description="생산 라인 AOI Station의 연결 및 가동 상태입니다." action={<button className="btn btn-ghost" onClick={load}><RefreshCw size={15} /> 자동 갱신 10s</button>} />
    {error && <div className="info-banner"><b>조회 실패</b><span>{error}</span></div>}
    <div className="station-summary"><span><i className="running" /> 가동 중 <b>{count('RUNNING')}</b></span><span><i className="stop" /> 정지 <b>{count('STOP')}</b></span><span><i className="error" /> 오류 <b>{count('ERROR')}</b></span><span><i className="offline" /> 오프라인 <b>{count('OFFLINE')}</b></span><small>마지막 갱신 {updatedAt?.toLocaleTimeString('ko-KR') || '-'}</small></div>
    {loading ? <LoadingState /> : <div className="station-grid">{stations.map((station) => <button className={`station-card station-${station.status.toLowerCase()}`} onClick={() => setSelected(station)} key={station.id}><div className="station-card-head"><div><span className="station-icon"><Radio size={20} /></span><div><h3>{station.id}</h3><p>{station.name}</p></div></div><StatusBadge value={station.status} dot /></div><div className="station-product"><span>현재 작업</span><b>{station.product}</b><small>Recipe {station.recipe}</small></div><div className="device-status"><div><Camera size={15} /><span>Camera</span><StatusBadge value={station.camera} dot /></div><div><Cpu size={15} /><span>AI Server</span><StatusBadge value={station.ai} dot /></div><div><Database size={15} /><span>MES</span><StatusBadge value={station.mes} dot /></div></div><div className="station-footer"><span>오늘 검사 <b>{station.today.toLocaleString()}건</b></span><span>{lastSeenLabel(station.lastSeen)}</span></div></button>)}</div>}
    <Modal open={!!selected} title={`${selected?.id} Station 상세`} onClose={() => setSelected(null)} footer={<button className="btn btn-primary" onClick={() => setSelected(null)}>확인</button>}><div className="detail-summary vertical"><div><span>라인 명</span><b>{selected?.name}</b></div><div><span>상태</span>{selected && <StatusBadge value={selected.status} />}</div><div><span>현재 제품 / Recipe</span><b>{selected?.product} / {selected?.recipe}</b></div><div><span>오늘 검사</span><b>{selected?.today.toLocaleString()}건</b></div><div><span>마지막 통신</span><b>{lastSeenLabel(selected?.lastSeen)}</b></div></div></Modal>
  </>;
}
