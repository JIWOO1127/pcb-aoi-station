import { useEffect, useState } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { recipeService } from '../services/api';
import { DataTable, LoadingState, PageHeader, Panel, StatusBadge } from '../components/UI';

const formatDate = (value) => value ? new Date(value).toLocaleString('ko-KR') : '-';

export default function RecipeDetailPage() {
  const { recipeId } = useParams();
  const navigate = useNavigate();
  const [recipe, setRecipe] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    recipeService.get(recipeId).then(setRecipe).catch((reason) => setError(reason.message));
  }, [recipeId]);

  const columns = [
    { key: 'partId', label: '부품 ID', render: (value) => <b>{value}</b> },
    { key: 'expectedClass', label: '예상 클래스', render: (value) => <code className="class-code">{value}</code> },
    { key: 'holes', label: '핀홀', render: (value) => value?.join(', ') || '-' },
  ];

  return <>
    <button className="back-button" onClick={() => navigate('/recipes')}><ArrowLeft size={16} /> 레시피 목록</button>
    {error && <div className="info-banner"><b>레시피 조회 실패</b><span>{error}</span></div>}
    {!recipe && !error ? <LoadingState /> : recipe ? <>
      <PageHeader
        eyebrow={`${recipe.productId} / RECIPE`}
        title={`${recipe.productId} v${recipe.version}`}
        description={`레시피 ID: ${recipe.recipeId}`}
        action={<StatusBadge value={recipe.active ? 'ACTIVE' : 'INACTIVE'} dot />}
      />
      <div className="detail-summary">
        <div><span>레시피 ID</span><b>{recipe.recipeId}</b></div>
        <div><span>제품</span><b>{recipe.productId}</b></div>
        <div><span>부품 수</span><b>{recipe.parts.length}</b></div>
        <div><span>생성 일시</span><b>{formatDate(recipe.createdAt)}</b></div>
      </div>
      <Panel title="비고" subtitle="레시피 변경 사유 및 참고 사항">
        <p>{recipe.note || '-'}</p>
      </Panel>
      <Panel title="부품 검사 기준" subtitle="부품별 예상 클래스와 핀홀">
        <DataTable columns={columns} rows={recipe.parts.map((part) => ({ ...part, id: part.partId }))} pageSize={20} />
      </Panel>
    </> : null}
  </>;
}
