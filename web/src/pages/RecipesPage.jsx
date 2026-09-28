import { useEffect, useMemo, useState } from 'react';
import { Camera, Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { recipeService } from '../services/api';
import {
  DataTable,
  Field,
  LoadingState,
  Modal,
  PageHeader,
  SearchFilter,
  StatusBadge,
} from '../components/UI';

const formatDate = (value) =>
  value ? new Date(value).toLocaleString('ko-KR') : '-';

const emptyPart = () => ({
  partId: '',
  expectedClass: '',
  holes: '',
});

export default function RecipesPage() {
  const [items, setItems] = useState([]);
  const [product, setProduct] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  // 새 레시피 작성 폼
  const [form, setForm] = useState(null);

  // empty: 빈 레시피 생성
  // copy: 기존 레시피 복제
  const [createMode, setCreateMode] = useState('copy');

  // 복제할 원본 Recipe ID
  const [sourceRecipeId, setSourceRecipeId] = useState('');

  const navigate = useNavigate();
  const { user } = useAuth();

  const canCreate = ['ADMIN', 'ENGINEER'].includes(user?.role);

  const load = async () => {
    setLoading(true);
    setError('');

    try {
      setItems(await recipeService.list());
    } catch (reason) {
      setError(reason.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const rows = useMemo(
    () =>
      items
        .map((item) => ({
          ...item,
          id: item.recipeId,
          status: item.status || (item.active ? 'ACTIVE' : 'INACTIVE'),
          createdAtLabel: formatDate(item.createdAt),
        }))
        .filter(
          (item) =>
            (!product || item.productId === product) &&
            (!status || item.status === status),
        ),
    [items, product, status],
  );

  const products = [...new Set(items.map((item) => item.productId))];

  const columns = [
    {
      key: 'recipeId',
      label: '레시피 ID',
      render: (value) => <b className="cell-id">{value}</b>,
    },
    {
      key: 'productId',
      label: '제품',
    },
    {
      key: 'version',
      label: '버전',
      render: (value, row) => (
        <span className={row.active ? 'version-active' : 'version-tag'}>
          v{value}
        </span>
      ),
    },
    {
      key: 'note',
      label: '비고',
      render: (value) => value || '-',
    },
    {
      key: 'status',
      label: '상태',
      render: (value) => <StatusBadge value={value} dot />,
    },
    {
      key: 'createdAtLabel',
      label: '생성 일시',
    },
  ];

  // 제품의 다음 Recipe 버전 계산
  const getNextVersion = (productId) => {
    const versions = items
      .filter((item) => item.productId === productId)
      .map((item) => Number(item.version));

    return Math.max(0, ...versions) + 1;
  };

  // 제품의 기본 복제 대상 Recipe 찾기
  // 1순위: ACTIVE
  // 2순위: 가장 높은 버전
  const getDefaultSource = (productId) => {
    const recipes = items
      .filter((item) => item.productId === productId)
      .sort((a, b) => Number(b.version) - Number(a.version));

    return recipes.find((item) => item.active) || recipes[0] || null;
  };

  // Recipe 상세를 읽어서 폼에 복사
  const copyRecipe = async (recipeId) => {
    if (!recipeId) return;

    setError('');

    try {
      const source = await recipeService.get(recipeId);

      setForm((current) => ({
        ...current,

        productId: source.productId,

        // 원본 버전을 그대로 쓰지 않고 다음 버전으로 생성
        version: getNextVersion(source.productId),

        // 비고는 새 버전의 변경 사유를 적도록 비움
        note: '',

        // 기존 부품 기준 복사
        parts:
          source.parts?.length > 0
            ? source.parts.map((part) => ({
              partId: part.partId || '',
              expectedClass: part.expectedClass || '',
              holes: Array.isArray(part.holes)
                ? part.holes.join(', ')
                : part.holes || '',
            }))
            : [emptyPart()],
      }));
    } catch (reason) {
      setError(reason.message);
    }
  };

  // 새 레시피 버튼
  const openCreate = async () => {
    const productId = product || products[0] || '';
    const source = getDefaultSource(productId);

    setError('');

    // 기존 Recipe가 있으면 복제를 기본 선택
    const mode = source ? 'copy' : 'empty';

    setCreateMode(mode);
    setSourceRecipeId(source?.recipeId || '');

    setForm({
      productId,
      version: getNextVersion(productId),
      note: '',
      parts: [emptyPart()],
    });

    // ACTIVE Recipe가 있으면 처음부터 값도 자동으로 가져옴
    if (source) {
      try {
        const detail = await recipeService.get(source.recipeId);

        setForm({
          productId: detail.productId,
          version: getNextVersion(detail.productId),
          note: '',
          parts:
            detail.parts?.length > 0
              ? detail.parts.map((part) => ({
                partId: part.partId || '',
                expectedClass: part.expectedClass || '',
                holes: Array.isArray(part.holes)
                  ? part.holes.join(', ')
                  : part.holes || '',
              }))
              : [emptyPart()],
        });
      } catch (reason) {
        setError(reason.message);
      }
    }
  };

  // 제품 변경
  const changeProduct = async (productId) => {
    const source = getDefaultSource(productId);

    setForm((current) => ({
      ...current,
      productId,
      version: getNextVersion(productId),
      parts: [emptyPart()],
    }));

    if (createMode === 'copy' && source) {
      setSourceRecipeId(source.recipeId);

      try {
        const detail = await recipeService.get(source.recipeId);

        setForm({
          productId,
          version: getNextVersion(productId),
          note: '',
          parts:
            detail.parts?.length > 0
              ? detail.parts.map((part) => ({
                partId: part.partId || '',
                expectedClass: part.expectedClass || '',
                holes: Array.isArray(part.holes)
                  ? part.holes.join(', ')
                  : part.holes || '',
              }))
              : [emptyPart()],
        });
      } catch (reason) {
        setError(reason.message);
      }
    } else {
      setSourceRecipeId('');
    }
  };

  // 빈 생성 / 기존 Recipe 복제 변경
  const changeCreateMode = async (mode) => {
    setCreateMode(mode);
    setError('');

    if (mode === 'empty') {
      setSourceRecipeId('');

      setForm((current) => ({
        ...current,
        parts: [emptyPart()],
      }));

      return;
    }

    const source = getDefaultSource(form.productId);

    if (!source) {
      setSourceRecipeId('');
      setForm((current) => ({
        ...current,
        parts: [emptyPart()],
      }));
      return;
    }

    setSourceRecipeId(source.recipeId);
    await copyRecipe(source.recipeId);
  };

  // 기준 Recipe 변경
  const changeSourceRecipe = async (recipeId) => {
    setSourceRecipeId(recipeId);

    if (!recipeId) {
      setForm((current) => ({
        ...current,
        parts: [emptyPart()],
      }));
      return;
    }

    // 선택하는 순간 자동 복사
    await copyRecipe(recipeId);
  };

  const updatePart = (index, key, value) => {
    setForm((current) => ({
      ...current,
      parts: current.parts.map((part, partIndex) =>
        partIndex === index
          ? { ...part, [key]: value }
          : part,
      ),
    }));
  };

  const save = async () => {
    const productId = form.productId.trim();

    const parts = form.parts.map((part) => ({
      partId: part.partId.trim(),
      expectedClass: part.expectedClass.trim(),
      holes: part.holes
        .split(',')
        .map((hole) => hole.trim().toUpperCase())
        .filter(Boolean),
    }));

    if (
      !productId ||
      !Number.isInteger(Number(form.version)) ||
      Number(form.version) < 1
    ) {
      setError('제품 ID와 1 이상의 버전을 입력해 주세요.');
      return;
    }

    if (
      !parts.length ||
      parts.some(
        (part) =>
          !part.partId ||
          !part.expectedClass ||
          !part.holes.length,
      )
    ) {
      setError('각 부품의 ID, 예상 클래스, 홀을 모두 입력해 주세요.');
      return;
    }

    setSaving(true);
    setError('');

    try {
      const created = await recipeService.create({
        productId,
        version: Number(form.version),
        note: form.note.trim() || null,
        parts,
      });

      setForm(null);

      navigate(`/recipes/${created.recipeId}`);
    } catch (reason) {
      setError(reason.message);
    } finally {
      setSaving(false);
    }
  };

  // 현재 제품에서 복제할 수 있는 Recipe 목록
  const copyCandidates = form
    ? items
      .filter((item) => item.productId === form.productId)
      .sort((a, b) => Number(b.version) - Number(a.version))
    : [];

  return (
    <>
      <PageHeader
        eyebrow="MASTER DATA"
        title="레시피 관리"
        description="제품별 검사 레시피와 부품 기준을 조회합니다."
        action={
          canCreate && <>
            <button className="btn btn-ghost" onClick={openCreate}><Plus size={16} /> 직접 등록</button>
            <button className="btn btn-primary" onClick={() => navigate('/recipes/photo/new')}><Camera size={16} /> 사진으로 등록</button>
          </>
        }
      />

      <SearchFilter
        onReset={() => {
          setProduct('');
          setStatus('');
        }}
      >
        <Field label="제품">
          <select
            value={product}
            onChange={(event) =>
              setProduct(event.target.value)
            }
          >
            <option value="">전체 제품</option>

            {products.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </Field>

        <Field label="상태">
          <select
            value={status}
            onChange={(event) =>
              setStatus(event.target.value)
            }
          >
            <option value="">전체 상태</option>
            <option value="ACTIVE">활성</option>
            <option value="DRAFT">초안</option>
            <option value="ARCHIVED">이전 버전</option>
          </select>
        </Field>
      </SearchFilter>

      {error && !form && (
        <div className="info-banner">
          <b>확인 필요</b>
          <span>{error}</span>
        </div>
      )}

      <div className="list-summary">
        <span>레시피 목록</span>
        <b>{rows.length}</b>
        <small>개 버전</small>
      </div>

      <div className="panel table-panel">
        {loading ? (
          <LoadingState />
        ) : (
          <DataTable
            columns={columns}
            rows={rows}
            onRowClick={(row) =>
              navigate(`/recipes/${row.recipeId}`)
            }
          />
        )}
      </div>

      <Modal
        open={!!form}
        title="새 레시피 만들기"
        onClose={() => !saving && setForm(null)}
        footer={
          <>
            <button
              className="btn btn-ghost"
              disabled={saving}
              onClick={() => setForm(null)}
            >
              취소
            </button>

            <button
              className="btn btn-primary"
              disabled={saving}
              onClick={save}
            >
              {saving ? '저장 중...' : '레시피 저장'}
            </button>
          </>
        }
      >
        {error && (
          <div className="info-banner">
            <b>저장 실패</b>
            <span>{error}</span>
          </div>
        )}

        <div className="form-grid two recipe-form">

          {/* 생성 방식 */}
          <Field label="생성 방식">
            <select
              value={createMode}
              onChange={(event) =>
                changeCreateMode(event.target.value)
              }
            >
              <option value="copy">
                기존 레시피에서 복제
              </option>

              <option value="empty">
                빈 레시피로 생성
              </option>
            </select>
          </Field>

          {/* 제품 */}
          <Field label="제품 ID">
            <input
              maxLength={50}
              value={form?.productId || ''}
              onChange={(event) =>
                setForm({
                  ...form,
                  productId: event.target.value,
                })
              }
              placeholder="NE555-B"
            />
          </Field>

          {/* 복제할 기존 Recipe */}
          {createMode === 'copy' && (
            <Field label="기준 레시피">
              <select
                value={sourceRecipeId}
                onChange={(event) =>
                  changeSourceRecipe(event.target.value)
                }
              >
                <option value="">
                  복제할 레시피 선택
                </option>

                {copyCandidates.map((recipe) => (
                  <option
                    key={recipe.recipeId}
                    value={recipe.recipeId}
                  >
                    v{recipe.version}
                    {` (${recipe.status || (recipe.active ? 'ACTIVE' : 'INACTIVE')})`}
                  </option>
                ))}
              </select>
            </Field>
          )}

          {/* 새 버전 */}
          <Field label="새 버전">
            <input
              type="number"
              min="1"
              value={form?.version || 1}
              onChange={(event) =>
                setForm({
                  ...form,
                  version: event.target.value,
                })
              }
            />
          </Field>

          {/* 비고 */}
          <Field label="비고">
            <textarea
              maxLength={2000}
              value={form?.note || ''}
              onChange={(event) =>
                setForm({
                  ...form,
                  note: event.target.value,
                })
              }
              placeholder="변경 사유나 참고 사항"
            />
          </Field>
        </div>

        <div className="recipe-parts-head">
          <div>
            <b>부품 기준</b>
            <span>
              기존 레시피를 복제하면 부품 기준이
              자동으로 입력됩니다.
            </span>
          </div>

          <button
            type="button"
            className="btn btn-ghost"
            onClick={() =>
              setForm({
                ...form,
                parts: [...form.parts, emptyPart()],
              })
            }
          >
            <Plus size={14} />
            부품 추가
          </button>
        </div>

        <div className="recipe-parts-editor">
          {form?.parts.map((part, index) => (
            <div
              className="recipe-part-row"
              key={index}
            >
              <input
                aria-label="부품 ID"
                maxLength={50}
                value={part.partId}
                onChange={(event) =>
                  updatePart(
                    index,
                    'partId',
                    event.target.value,
                  )
                }
                placeholder="R1"
              />

              <input
                aria-label="예상 클래스"
                maxLength={100}
                value={part.expectedClass}
                onChange={(event) =>
                  updatePart(
                    index,
                    'expectedClass',
                    event.target.value,
                  )
                }
                placeholder="resistance330"
              />

              <input
                aria-label="홀 목록"
                value={part.holes}
                onChange={(event) =>
                  updatePart(
                    index,
                    'holes',
                    event.target.value,
                  )
                }
                placeholder="E9, H9"
              />

              <button
                type="button"
                className="icon-btn"
                aria-label="부품 삭제"
                disabled={form.parts.length === 1}
                onClick={() =>
                  setForm({
                    ...form,
                    parts: form.parts.filter(
                      (_, partIndex) =>
                        partIndex !== index,
                    ),
                  })
                }
              >
                <Trash2 size={16} />
              </button>
            </div>
          ))}
        </div>
      </Modal>
    </>
  );
}
