import { useEffect, useState } from 'react';
import { ArrowLeft, Check } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { recipeService } from '../services/api';
import { ConfirmModal, DataTable, LoadingState, PageHeader, Panel, StatusBadge } from '../components/UI';

const formatDate = (value) => value ? new Date(value).toLocaleString('ko-KR') : '-';

export default function RecipeDetailPage() {
  const { recipeId } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [recipe, setRecipe] = useState(null);
  const [error, setError] = useState('');
  const [confirmApproval, setConfirmApproval] = useState(false);
  const [approving, setApproving] = useState(false);

  useEffect(() => {
    recipeService.get(recipeId).then(setRecipe).catch((reason) => setError(reason.message));
  }, [recipeId]);

  const columns = [
    { key: 'partId', label: '부품 ID', render: (value) => <b>{value}</b> },
    { key: 'expectedClass', label: '예상 클래스', render: (value) => <code className="class-code">{value}</code> },
    { key: 'holes', label: '핀홀', render: (value) => value?.join(', ') || '-' },
  ];

  const approve = async () => {
    setApproving(true);
    setError('');
    try {
      setRecipe(await recipeService.approve(recipeId));
      setConfirmApproval(false);
    } catch (reason) {
      setError(reason.message);
    } finally {
      setApproving(false);
    }
  };

  const isDraft = recipe?.status === 'DRAFT';

  return <>
    <button className="back-button" onClick={() => navigate('/recipes')}><ArrowLeft size={16} /> 레시피 목록</button>
    {error && <div className="info-banner"><b>레시피 조회 실패</b><span>{error}</span></div>}
    {!recipe ? <LoadingState /> : <>
      <PageHeader
        eyebrow={`${recipe.productId} / RECIPE`}
        title={`${recipe.productId} v${recipe.version}`}
        description={`레시피 ID: ${recipe.recipeId}`}
        action={<div className="row-actions">
          <StatusBadge value={recipe.status || (recipe.active ? 'ACTIVE' : 'INACTIVE')} dot />
          {isDraft && user?.role === 'ADMIN' && (
            <button className="btn btn-primary" onClick={() => setConfirmApproval(true)}>
              <Check size={15} /> 승인 및 활성화
            </button>
          )}
        </div>}
      />
      {isDraft && (
        <div className="info-banner">
          <b>승인 대기</b>
          <span>{user?.role === 'ADMIN'
            ? '내용을 확인한 뒤 승인하면 이 버전이 활성화되고 기존 활성 버전은 이전 버전으로 변경됩니다.'
            : '사진으로 등록한 레시피는 관리자가 승인한 뒤 활성 레시피로 적용됩니다.'}</span>
        </div>
      )}
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
      <ConfirmModal
        open={confirmApproval}
        title="레시피 승인 및 활성화"
        message={`${recipe.productId} v${recipe.version}을 활성 레시피로 적용하시겠습니까? 기존 활성 버전은 이전 버전으로 변경됩니다.`}
        onClose={() => !approving && setConfirmApproval(false)}
        onConfirm={approve}
        confirmText={approving ? '승인 중...' : '승인 및 활성화'}
      />
    </>}
  </>;
}
