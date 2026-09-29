import { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, Check, ImagePlus, RotateCcw, Save, Trash2, Undo2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { productService, recipeDraftService, recipeService } from '../services/api';
import { Field, LoadingState, PageHeader, StatusBadge } from '../components/UI';
import '../recipe-draft.css';

const CLASSES = ['ic_chip', 'resistance330', 'resistance470', 'capacitor', 'led', 'black_wire', 'red_wire', 'white_wire'];
const REQUIRED_HOLES = { ic_chip: 14, resistance330: 2, resistance470: 2, capacitor: 2, led: 2, black_wire: 2, red_wire: 2, white_wire: 2 };
const HOLE_PATTERN = /^[A-U](1[0-3]|[0-9])$/;
const REASON_LABELS = { LOW_CONFIDENCE: '낮은 신뢰도', HOLE_COUNT: '홀 개수', HOLE_OVERLAP: '홀 중복' };

const parseHoles = (value) => value.split(',').map((hole) => hole.trim().toUpperCase()).filter(Boolean);

function currentErrors(parts) {
  const errors = {};
  const owners = new Map();
  parts.forEach((part) => {
    const messages = [];
    if (!part.partId.trim()) messages.push('부품 ID 필요');
    if (!CLASSES.includes(part.class)) messages.push('허용되지 않은 class');
    const holes = parseHoles(part.holesText);
    if (holes.length !== REQUIRED_HOLES[part.class]) messages.push(`홀 ${REQUIRED_HOLES[part.class] || 0}개 필요`);
    if (holes.some((hole) => !HOLE_PATTERN.test(hole))) messages.push('홀 형식 A0~U13');
    if (new Set(holes).size !== holes.length) messages.push('부품 내 홀 중복');
    holes.forEach((hole) => {
      if (owners.has(hole)) {
        messages.push(`${hole} 중복`);
        errors[owners.get(hole)] = [...(errors[owners.get(hole)] || []), `${hole} 중복`];
      } else owners.set(hole, part.key);
    });
    errors[part.key] = [...(errors[part.key] || []), ...messages];
  });
  return errors;
}

function BoardPreview({ draft, parts, mode }) {
  const overlay = draft.overlay || {};
  const width = overlay.width || 640;
  const height = overlay.height || 640;
  const holes = overlay.holes || [];
  const holeMap = new Map(holes.map((hole) => [hole.label, hole]));
  const imageUrl = overlay.image
    ? (overlay.image.startsWith('data:') ? overlay.image : `data:image/jpeg;base64,${overlay.image}`)
    : '';

  const partBox = (part, boxMode) => {
    if (boxMode === 'photo') return part.box;

    const points = parseHoles(part.holesText)
      .map((hole) => holeMap.get(hole))
      .filter(Boolean);
    if (!points.length) return null;

    const xs = points.map((point) => point.x);
    const ys = points.map((point) => point.y);
    const minX = Math.min(...xs);
    const maxX = Math.max(...xs);
    const minY = Math.min(...ys);
    const maxY = Math.max(...ys);
    const padding = 10;

    return {
      x: minX - padding,
      y: minY - padding,
      w: Math.max(24, maxX - minX + padding * 2),
      h: Math.max(24, maxY - minY + padding * 2),
    };
  };

  const renderBoxes = (boxMode) => parts.map((part) => {
    const box = partBox(part, boxMode);
    if (!box) return null;
    const needsReview = part.needsReview;

    return (
      <g
        key={part.key}
        className={`draft-part ${boxMode === 'photo' ? 'draft-photo-part' : `class-${part.class}`} ${needsReview ? 'review' : ''}`}
      >
        <rect
          x={box.x}
          y={box.y}
          width={box.w}
          height={box.h}
          rx="3"
        />
        <text x={box.x + 3} y={Math.max(11, box.y - 4)}>{part.partId}</text>
      </g>
    );
  });

  if (mode === 'photo') {
    if (!imageUrl) {
      return <div className="draft-board-missing">분석된 보드 사진이 응답에 없습니다.</div>;
    }

    return (
      <div className="draft-board-wrap">
        <div className="draft-photo-overlay" style={{ aspectRatio: `${width} / ${height}` }}>
          <img src={imageUrl} alt="분석된 기준 보드" />
          <svg
            viewBox={`0 0 ${width} ${height}`}
            preserveAspectRatio="none"
            role="img"
            aria-label="부품 검출 박스"
          >
            {renderBoxes('photo')}
          </svg>
        </div>
      </div>
    );
  }

  return (
    <div className="draft-board-wrap">
      <svg
        className="draft-board"
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="xMidYMid meet"
        role="img"
        aria-label="레시피 분석 배치도"
      >
        <rect x="0" y="0" width={width} height={height} fill="#f7f9fc" />
        {holes.map((hole) => (
          <g key={hole.label}>
            <circle cx={hole.x} cy={hole.y} r="3.5" fill="#98a5b8" />
            <text x={hole.x + 5} y={hole.y - 4} fontSize="7" fill="#728096">{hole.label}</text>
          </g>
        ))}
        {renderBoxes('layout')}
      </svg>
    </div>
  );
}

export default function RecipeDraftPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const inputRef = useRef(null);
  const [products, setProducts] = useState([]);
  const [recipes, setRecipes] = useState([]);
  const [productId, setProductId] = useState('');
  const [file, setFile] = useState(null);
  const [previewUrl, setPreviewUrl] = useState('');
  const [note, setNote] = useState('');
  const [step, setStep] = useState('upload');
  const [draft, setDraft] = useState(null);
  const [parts, setParts] = useState([]);
  const [deleted, setDeleted] = useState([]);
  const [mode, setMode] = useState('layout');
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    Promise.all([productService.list(), recipeService.list()]).then(([productItems, recipeItems]) => {
      const active = productItems.filter((item) => item.status === 'ACTIVE');
      setProducts(active); setRecipes(recipeItems); setProductId(active[0]?.id || '');
    }).catch((reason) => setError(reason.message));
  }, []);
  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl); }, [previewUrl]);

  const activeRecipe = recipes.find((recipe) => recipe.productId === productId && recipe.active);
  const expectedVersion = Math.max(0, ...recipes.filter((recipe) => recipe.productId === productId).map((recipe) => Number(recipe.version))) + 1;
  const errors = useMemo(() => currentErrors(parts), [parts]);
  const unresolved = parts.filter((part) => part.needsReview && !part.reviewConfirmed);
  const hasErrors = Object.values(errors).some((values) => values.length);
  const canApprove = parts.length > 0 && !hasErrors && unresolved.length === 0;

  const chooseFile = (selected) => {
    if (!selected) return;
    if (!['image/jpeg', 'image/png'].includes(selected.type)) { setError('JPEG 또는 PNG 사진만 선택해 주세요.'); return; }
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setFile(selected); setPreviewUrl(URL.createObjectURL(selected)); setError('');
  };

  const analyze = async () => {
    if (!productId || !file) { setError('제품과 기준 보드 사진을 선택해 주세요.'); return; }
    setBusy(true); setError(''); setStep('analyzing');
    try {
      const response = await recipeDraftService.analyze({ productId, image: file });
      setDraft(response);
      setParts((response.parts || []).map((part, index) => ({ ...part, key: `${part.partId}-${index}`,
        holesText: (part.holes || []).join(', '), reviewConfirmed: !part.needsReview })));
      setDeleted([]); setStep('review');
    } catch (reason) { setError(reason.message); setStep('upload'); }
    finally { setBusy(false); }
  };

  const updatePart = (key, changes) => setParts((items) => items.map((part) => part.key === key
    ? { ...part, ...changes, reviewConfirmed: changes.reviewConfirmed ?? false } : part));
  const removePart = (key) => setParts((items) => {
    const target = items.find((part) => part.key === key); if (target) setDeleted((old) => [...old, target]);
    return items.filter((part) => part.key !== key);
  });
  const undo = (key) => setDeleted((items) => {
    const target = items.find((part) => part.key === key); if (target) setParts((old) => [...old, target]);
    return items.filter((part) => part.key !== key);
  });

  const payload = () => ({ productId, note: note.trim() || null, parts: parts.map((part) => ({
    partId: part.partId.trim(), class: part.class, holes: parseHoles(part.holesText), confidence: part.confidence,
    needsReview: part.needsReview, reviewConfirmed: part.reviewConfirmed, reasons: part.reasons || [],
  })) });

  const saveDraft = async () => {
    if (!parts.length || hasErrors) { setError('부품 class와 홀 형식을 확인해 주세요.'); return null; }
    setBusy(true); setError('');
    try { const result = saved || await recipeDraftService.save(payload()); setSaved(result); return result; }
    catch (reason) { setError(reason.message); return null; }
    finally { setBusy(false); }
  };
  const approve = async () => {
    if (!canApprove) { setError('검토 필요 부품을 확인하거나 삭제하고 홀 검증 오류를 해결해 주세요.'); return; }
    const recipe = await saveDraft(); if (!recipe) return;
    setBusy(true);
    try { const result = await recipeDraftService.approve(recipe.recipeId); setSaved(result); setStep('done'); }
    catch (reason) { setError(reason.message); }
    finally { setBusy(false); }
  };

  if (step === 'analyzing') return <><PageHeader eyebrow="RECIPE DRAFT" title="사진 분석 중" description="부품과 브레드보드 홀을 인식하고 있습니다." /><div className="panel"><LoadingState /></div></>;
  if (step === 'done') return <><PageHeader eyebrow="RECIPE APPROVED" title="레시피 승인이 완료되었습니다" description={`${saved.productId} v${saved.version}이 활성 레시피가 되었습니다.`} /><div className="draft-done"><Check size={42} /><StatusBadge value="ACTIVE" dot /><button className="btn btn-primary" onClick={() => navigate(`/recipes/${saved.recipeId}`)}>레시피 상세 보기</button></div></>;

  return <>
    <button className="back-button" onClick={() => navigate('/recipes')}><ArrowLeft size={16} /> 레시피 목록</button>
    <PageHeader eyebrow="RECIPE DRAFT" title="사진으로 새 레시피 등록" description="기준 보드 사진을 분석한 뒤 부품 class와 홀을 검토합니다." />
    <div className="draft-steps"><b className={step === 'upload' ? 'active' : 'done'}>1 사진 업로드</b><b className={step === 'review' ? 'active' : ''}>2 분석 결과 검토</b><b>3 저장 및 승인</b></div>
    {error && <div className="info-banner draft-error"><b>확인 필요</b><span>{error}</span></div>}

    {step === 'upload' && <div className="draft-upload-grid">
      <section className="panel draft-form-card"><h2>기준 정보</h2>
        <Field label="제품"><select value={productId} onChange={(event) => setProductId(event.target.value)}><option value="">제품 선택</option>{products.map((product) => <option key={product.id} value={product.id}>{product.id} · {product.name}</option>)}</select></Field>
        <div className="draft-active-recipe"><span>현재 활성 레시피</span><b>{activeRecipe ? `v${activeRecipe.version}` : '없음'}</b><small>새 초안 예상 버전 v{expectedVersion}</small></div>
        <Field label="비고"><textarea value={note} maxLength={2000} onChange={(event) => setNote(event.target.value)} placeholder="변경 사유나 참고 사항" /></Field>
      </section>
      <section className="panel draft-photo-card"><h2>기준 보드 사진</h2>
        <input ref={inputRef} hidden type="file" accept="image/jpeg,image/png" onChange={(event) => chooseFile(event.target.files?.[0])} />
        <button className={`draft-dropzone ${previewUrl ? 'has-image' : ''}`} onClick={() => inputRef.current?.click()} onDragOver={(event) => event.preventDefault()} onDrop={(event) => { event.preventDefault(); chooseFile(event.dataTransfer.files?.[0]); }}>
          {previewUrl ? <img src={previewUrl} alt="업로드할 기준 보드" /> : <><ImagePlus size={34} /><b>사진을 선택하거나 여기에 놓으세요</b><span>JPEG, PNG · 최대 10MB</span></>}
        </button>
        <ul className="capture-guide"><li>보드 전체가 프레임 안에 들어오게 촬영</li><li>수직에 가깝게 촬영하고 그림자와 반사 최소화</li><li>부품과 홀 번호가 선명한 사진 사용</li></ul>
        <button className="btn btn-primary draft-main-action" disabled={busy || !file || !productId} onClick={analyze}>사진 분석</button>
      </section>
    </div>}

    {step === 'review' && <>
      <div className="draft-review-head"><div><b>{productId}</b><span>초안 v{saved?.version || expectedVersion} · 부품 {parts.length}개 · 검토 필요 {unresolved.length}개</span></div><div className="draft-toggle"><button className={mode === 'photo' ? 'active' : ''} onClick={() => setMode('photo')}>사진</button><button className={mode === 'layout' ? 'active' : ''} onClick={() => setMode('layout')}>배치도</button></div></div>
      {(draft.warnings || []).map((warning) => <div className="draft-warning" key={warning}>{warning}</div>)}
      {parts.some((part) => part.class?.startsWith('resistance')) && <div className="draft-warning">저항은 조명에 따라 class가 오인식될 수 있습니다. 색상띠와 class를 직접 확인해 주세요.</div>}
      <div className="draft-review-grid">
        <section className="panel draft-preview-panel"><BoardPreview draft={draft} parts={parts} mode={mode} /><div className="draft-legend">
          {/* <span><i />확인 완료</span><span><i className="red" />검토 필요</span> */}
          </div></section>
        <section className="panel draft-parts-panel">
  <div className="draft-parts-title">
    <div>
      <h2>부품 목록</h2>
      <span>
        총 {parts.length}개 · 검토 필요 {unresolved.length}개
      </span>
    </div>

    <span>class와 holes를 직접 수정할 수 있습니다.</span>
  </div>

  <div className="draft-parts-table-head">
    <span>ID</span>
    <span>Class</span>
    <span>Holes</span>
    <span>신뢰도</span>
    <span></span>
  </div>

  <div className="draft-parts-list">
    {parts.map((part) => {
      const confidence = Math.round((part.confidence || 0) * 100);
      const needsReview =
        part.needsReview && !part.reviewConfirmed;

      return (
        <div
          className={`draft-part-card ${
            needsReview ? 'needs-review' : ''
          }`}
          key={part.key}
        >
          <div className="draft-part-main-row">
            <div className={`draft-part-id-wrap class-${part.class}`}>
  <span className="draft-class-dot" />

  <input
    className="draft-part-id"
    aria-label="부품 ID"
    value={part.partId}
    onChange={(event) =>
      updatePart(part.key, {
        partId: event.target.value,
      })
    }
  />
</div>

            <select
              aria-label="class"
              value={part.class}
              onChange={(event) =>
                updatePart(part.key, {
                  class: event.target.value,
                })
              }
            >
              {CLASSES.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>

            <input
              className="draft-part-holes"
              aria-label="홀 목록"
              value={part.holesText}
              onChange={(event) =>
                updatePart(part.key, {
                  holesText: event.target.value,
                })
              }
              placeholder="E9, H9"
            />

            <span
              className={`draft-confidence ${
                confidence < 50
                  ? 'low'
                  : confidence < 70
                    ? 'medium'
                    : 'high'
              }`}
            >
              {confidence}%
            </span>

            <button
              className="icon-btn"
              aria-label="부품 삭제"
              onClick={() => removePart(part.key)}
            >
              <Trash2 size={15} />
            </button>
          </div>

          {((part.reasons || []).length > 0 ||
            (errors[part.key] || []).length > 0 ||
            part.needsReview) && (
            <div className="draft-part-detail">
              <div className="draft-part-messages">
                {(part.reasons || []).length > 0 && (
                  <div className="draft-reasons">
                    {part.reasons.map((reason) => (
                      <span key={reason}>
                        {REASON_LABELS[reason] || reason}
                      </span>
                    ))}
                  </div>
                )}

                {(errors[part.key] || []).length > 0 && (
                  <small className="draft-validation">
                    {[...new Set(errors[part.key])].join(' · ')}
                  </small>
                )}
              </div>

              {part.needsReview && (
                <button
                  className={`draft-confirm ${
                    part.reviewConfirmed ? 'confirmed' : ''
                  }`}
                  onClick={() =>
                    updatePart(part.key, {
                      reviewConfirmed: !part.reviewConfirmed,
                    })
                  }
                >
                  <Check size={13} />
                  {part.reviewConfirmed
                    ? '확인 완료'
                    : '이 부품 확인'}
                </button>
              )}
            </div>
          )}
        </div>
      );
    })}
  </div>

  {deleted.length > 0 && (
    <div className="draft-deleted">
      <b>삭제한 부품</b>

      {deleted.map((part) => (
        <button
          key={part.key}
          onClick={() => undo(part.key)}
        >
          <Undo2 size={13} />
          {part.partId} 복원
        </button>
      ))}
    </div>
  )}
</section>
      </div>
      <div className="draft-actions"><button className="btn btn-ghost" disabled={busy} onClick={() => { setStep('upload'); setDraft(null); setSaved(null); }}><RotateCcw size={15} /> 다시 촬영</button><span />
        {user?.role !== 'ADMIN' && <small>초안 저장 후 관리자가 승인하면 활성 레시피로 적용됩니다.</small>}
        <button className="btn btn-ghost" disabled={busy || !!saved} onClick={async () => { const result = await saveDraft(); if (result) navigate(`/recipes/${result.recipeId}`); }}><Save size={15} /> 초안 저장</button>
        {user?.role === 'ADMIN' && <button className="btn btn-primary" disabled={busy || !canApprove} onClick={approve}><Check size={15} /> 승인 및 활성화</button>}
      </div>
    </>}
  </>;
}
