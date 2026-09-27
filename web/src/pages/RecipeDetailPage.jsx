import { useState } from 'react';
import { ArrowLeft, CheckCircle2, Copy, Edit3 } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { components, recipes } from '../mocks/data';
import { ConfirmModal, DataTable, PageHeader, Panel, PcbBoard, StatusBadge } from '../components/UI';

export default function RecipeDetailPage() {
  const { recipeId } = useParams(); const navigate = useNavigate();
  const recipe = recipes.find((item) => item.id === recipeId) || recipes[0];
  const [confirm, setConfirm] = useState(false); const [active, setActive] = useState(recipe.status === 'ACTIVE');
  const columns = [{ key: 'className', label: 'Class Name', render: (v) => <code className="class-code">{v}</code> }, { key: 'count', label: '예상 수량' }, { key: 'position', label: '기준 위치' }, { key: 'tolerance', label: '허용 오차' }];
  const history = [{ version: 'v3.3', status: active ? 'ACTIVE' : 'DRAFT', date: '2026-09-26' }, { version: 'v3.2', status: active ? 'INACTIVE' : 'ACTIVE', date: '2026-09-21' }, { version: 'v3.1', status: 'INACTIVE', date: '2026-09-04' }, { version: 'v3.0', status: 'INACTIVE', date: '2026-08-18' }];
  return <><button className="back-button" onClick={() => navigate('/recipes')}><ArrowLeft size={16} /> Recipe 목록</button><PageHeader eyebrow={`${recipe.product} / RECIPE`} title={`${recipe.name} ${recipe.version}`} description={`Recipe ID ${recipe.id} · 작성자 ${recipe.createdBy}`} action={<div className="button-row"><button className="btn btn-ghost"><Copy size={15} /> 새 버전 복제</button><button className="btn btn-ghost"><Edit3 size={15} /> 편집</button>{!active && <button className="btn btn-primary" onClick={() => setConfirm(true)}><CheckCircle2 size={16} /> 적용</button>}</div>} />
    <div className="detail-summary"><div><span>현재 상태</span><StatusBadge value={active ? 'ACTIVE' : recipe.status} dot /></div><div><span>대상 제품</span><b>{recipe.product}</b></div><div><span>검출 클래스</span><b>8 Classes</b></div><div><span>최종 수정</span><b>{recipe.createdAt}</b></div></div>
    <div className="recipe-layout"><Panel title="PCB 기준 이미지" subtitle="Expected Component 영역"><PcbBoard /><div className="board-legend"><span><i className="green" /> Expected bounding box</span><span>Image 1280 × 960</span></div></Panel><Panel title="Version History" subtitle="제품별 Recipe 적용 이력"><div className="version-timeline">{history.map((item) => <div className={item.status === 'ACTIVE' ? 'current' : ''} key={item.version}><i /><b>{item.version}</b><StatusBadge value={item.status} /><span>{item.date}</span></div>)}</div></Panel></div>
    <Panel title="Expected Component" subtitle="검사 기준 부품 위치와 허용 오차"><DataTable columns={columns} rows={components.map((item, i) => ({ ...item, id: i }))} pageSize={8} /></Panel>
    <ConfirmModal open={confirm} title="Recipe 적용" message={`${recipe.version}을 ACTIVE Recipe로 적용하시겠습니까? 기존 ACTIVE 버전은 비활성화됩니다.`} onClose={() => setConfirm(false)} onConfirm={() => { setActive(true); setConfirm(false); }} confirmText="적용" />
  </>;
}
