import { useEffect, useState } from 'react';
import { Cpu, Plus } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { modelService } from '../services/api';
import { ConfirmModal, DataTable, Field, LoadingState, Modal, PageHeader, Panel, StatusBadge } from '../components/UI';

const emptyModel = { id: '', name: '', version: '', framework: 'MMDetection', classes: 8, input: '640 × 640', map50: '', map95: '', note: '' };

export function ModelsPage() {
  const [items, setItems] = useState([]);
  const [form, setForm] = useState(null);
  const [activate, setActivate] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const { user } = useAuth();
  const canEdit = ['ADMIN', 'ENGINEER'].includes(user?.role);
  const load = async () => { setLoading(true); try { setItems(await modelService.list()); setError(''); } catch (reason) { setError(reason.message); } finally { setLoading(false); } };
  useEffect(() => { load(); }, []);
  const active = items.find((item) => item.status === 'ACTIVE');
  const columns = [{ key: 'name', label: 'Model Name', render: (value) => <b>{value}</b> }, { key: 'version', label: 'Version', render: (value) => <span className="version-tag">{value}</span> }, { key: 'framework', label: 'Framework' }, { key: 'createdAt', label: 'Created At' }, { key: 'status', label: 'Status', render: (value) => <StatusBadge value={value} dot /> }, { key: 'note', label: 'Note' }, ...(canEdit ? [{ key: 'action', label: '관리', render: (_, row) => row.status !== 'ACTIVE' ? <button className="mini-primary" onClick={(event) => { event.stopPropagation(); setActivate(row); }}>활성화</button> : <span className="muted">배포 중</span> }] : [])];

  const save = async () => {
    if (!form.id.trim() || !form.name.trim() || !form.version.trim()) { setError('모델 ID, 이름, 버전을 입력해 주세요.'); return; }
    setSaving(true); setError('');
    try { await modelService.create({ ...form, classes: Number(form.classes), map50: form.map50 === '' ? null : Number(form.map50), map95: form.map95 === '' ? null : Number(form.map95), classNames: [] }); setForm(null); await load(); }
    catch (reason) { setError(reason.message); } finally { setSaving(false); }
  };
  const activateSelected = async () => { setSaving(true); try { await modelService.activate(activate.id); setActivate(null); await load(); } catch (reason) { setError(reason.message); } finally { setSaving(false); } };

  return <><PageHeader eyebrow="AI MANAGEMENT" title="AI 모델 관리" description="검출 모델의 성능 지표와 배포 버전을 관리합니다." action={canEdit && <button className="btn btn-primary" onClick={() => { setError(''); setForm({ ...emptyModel }); }}><Plus size={16} /> 모델 등록</button>} />
    {error && !form && <div className="info-banner"><b>확인 필요</b><span>{error}</span></div>}
    {active && <div className="model-hero"><div className="model-hero-icon"><Cpu size={30} /></div><div><p>CURRENT PRODUCTION MODEL</p><h2>{active.name} <span>{active.version}</span></h2><small>{active.framework} · {active.classes} Classes · Input {active.input}</small></div><div className="metric"><span>mAP50</span><b>{active.map50 ?? '-'}</b></div><div className="metric"><span>mAP50:95</span><b>{active.map95 ?? '-'}</b></div><StatusBadge value="ACTIVE" dot /></div>}
    <Panel title="등록 모델" subtitle="모델 메타데이터 및 배포 상태">{loading ? <LoadingState /> : <DataTable columns={columns} rows={items} onRowClick={(row) => navigate(`/models/${row.id}`)} />}</Panel>
    <Modal open={!!form} title="AI 모델 등록" onClose={() => !saving && setForm(null)} footer={<><button className="btn btn-ghost" disabled={saving} onClick={() => setForm(null)}>취소</button><button className="btn btn-primary" disabled={saving} onClick={save}>{saving ? '등록 중...' : '등록'}</button></>}>
      {error && <div className="info-banner"><b>등록 실패</b><span>{error}</span></div>}
      <div className="form-grid two"><Field label="Model ID"><input value={form?.id || ''} onChange={(event) => setForm({ ...form, id: event.target.value })} placeholder="MDL-025" /></Field><Field label="Model Name"><input value={form?.name || ''} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="RTMDet-m" /></Field><Field label="Version"><input value={form?.version || ''} onChange={(event) => setForm({ ...form, version: event.target.value })} placeholder="v2.5" /></Field><Field label="Framework"><select value={form?.framework || 'MMDetection'} onChange={(event) => setForm({ ...form, framework: event.target.value })}><option>MMDetection</option><option>Ultralytics</option></select></Field><Field label="Classes"><input type="number" min="0" value={form?.classes || 0} onChange={(event) => setForm({ ...form, classes: event.target.value })} /></Field><Field label="Input Size"><input value={form?.input || ''} onChange={(event) => setForm({ ...form, input: event.target.value })} /></Field><Field label="mAP50"><input type="number" step="0.001" value={form?.map50 || ''} onChange={(event) => setForm({ ...form, map50: event.target.value })} /></Field><Field label="mAP50:95"><input type="number" step="0.001" value={form?.map95 || ''} onChange={(event) => setForm({ ...form, map95: event.target.value })} /></Field><Field label="Note"><textarea value={form?.note || ''} onChange={(event) => setForm({ ...form, note: event.target.value })} /></Field></div>
    </Modal>
    <ConfirmModal open={!!activate} title="모델 활성화" message={`${activate?.name} ${activate?.version}을 생산 ACTIVE 모델로 설정하시겠습니까?`} onClose={() => !saving && setActivate(null)} onConfirm={activateSelected} confirmText="활성화" />
  </>;
}

export function ModelDetailPage() {
  const { modelId } = useParams();
  const navigate = useNavigate();
  const [model, setModel] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => { modelService.get(modelId).then(setModel).catch((reason) => setError(reason.message)); }, [modelId]);
  if (error) return <div className="info-banner"><b>모델 조회 실패</b><span>{error}</span></div>;
  if (!model) return <LoadingState />;
  return <><button className="back-button" onClick={() => navigate('/models')}>← 모델 목록</button><PageHeader eyebrow="MODEL DETAIL" title={`${model.name} ${model.version}`} description={model.note} action={<StatusBadge value={model.status} dot />} /><div className="model-detail-grid">{[['Framework', model.framework], ['Number of Classes', `${model.classes} Classes`], ['Input Size', model.input], ['mAP50', model.map50 ?? '-'], ['mAP50:95', model.map95 ?? '-'], ['Created At', model.createdAt]].map(([label, value]) => <div key={label}><span>{label}</span><b>{value}</b></div>)}</div><Panel title="검출 Class" subtitle="모델 학습 및 평가 대상"><div className="class-chips">{model.classNames.map((name) => <code key={name}>{name}</code>)}</div></Panel><div className="info-banner"><b>모델 파일 정책</b><span>MES는 모델 메타데이터와 버전·배포 이력만 관리합니다.</span></div></>;
}
