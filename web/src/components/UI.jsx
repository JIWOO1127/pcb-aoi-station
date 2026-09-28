import { useEffect, useMemo, useState } from 'react';
import { ChevronLeft, ChevronRight, Search, RotateCcw, X } from 'lucide-react';

const statusLabels = {
  ACTIVE: '활성', INACTIVE: '비활성', DRAFT: '초안', PASS: 'PASS', FAIL: 'FAIL',
  RUNNING: '가동 중', STOP: '정지', ERROR: '오류', OFFLINE: '오프라인', ONLINE: '정상',
  ADMIN: '관리자', ENGINEER: '엔지니어', VIEWER: '조회자',
};

export function StatusBadge({ value, dot = false }) {
  return <span className={`status-badge status-${String(value).toLowerCase()}`}>{dot && <i />}{statusLabels[value] || value}</span>;
}

export function PageHeader({ eyebrow, title, description, action }) {
  return <div className="page-header"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1>{description && <p className="page-description">{description}</p>}</div>{action && <div className="page-actions">{action}</div>}</div>;
}

export function StatCard({ label, value, unit, icon: Icon, tone = 'blue', trend, caption }) {
  return <div className={`stat-card tone-${tone}`}><div className="stat-top"><span>{label}</span><span className="stat-icon">{Icon && <Icon size={18} />}</span></div><div className="stat-value">{value}<small>{unit}</small></div><div className="stat-meta">{trend && <b>{trend}</b>} {caption}</div></div>;
}

export function Panel({ title, subtitle, action, children, className = '' }) {
  return <section className={`panel ${className}`}><div className="panel-head"><div><h2>{title}</h2>{subtitle && <p>{subtitle}</p>}</div>{action}</div><div className="panel-body">{children}</div></section>;
}

export function SearchFilter({ children, onReset, onSearch }) {
  return <div className="search-filter"><div className="filter-fields">{children}</div><div className="filter-actions"><button className="btn btn-ghost" onClick={onReset}><RotateCcw size={15} /> 초기화</button>{onSearch && <button className="btn btn-primary" onClick={onSearch}><Search size={15} /> 조회</button>}</div></div>;
}

export function Field({ label, children }) {
  return <label className="field"><span>{label}</span>{children}</label>;
}

export function DataTable({ columns, rows, onRowClick, pageSize = 6, emptyText = '조회된 데이터가 없습니다.' }) {
  const [page, setPage] = useState(1);
  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
  useEffect(() => { setPage(1); }, [rows, pageSize]);
  const shown = useMemo(() => rows.slice((page - 1) * pageSize, page * pageSize), [rows, page, pageSize]);
  return <div className="table-wrap"><table className="data-table"><thead><tr>{columns.map((col) => <th key={col.key} style={{ width: col.width }}>{col.label}</th>)}</tr></thead><tbody>{shown.length ? shown.map((row, index) => <tr key={row.id || row.employeeId || index} className={onRowClick ? 'clickable' : ''} onClick={() => onRowClick?.(row)}>{columns.map((col) => <td key={col.key}>{col.render ? col.render(row[col.key], row) : row[col.key]}</td>)}</tr>) : <tr><td colSpan={columns.length}><EmptyState text={emptyText} /></td></tr>}</tbody></table>{rows.length > pageSize && <div className="pagination"><span>총 {rows.length}건</span><div><button disabled={page === 1} onClick={() => setPage((p) => p - 1)}><ChevronLeft size={16} /></button><b>{page}</b><span>/ {totalPages}</span><button disabled={page === totalPages} onClick={() => setPage((p) => p + 1)}><ChevronRight size={16} /></button></div></div>}</div>;
}

export function Modal({ open, title, children, onClose, footer }) {
  if (!open) return null;
  return <div className="modal-backdrop" onMouseDown={onClose}><div className="modal" onMouseDown={(event) => event.stopPropagation()}><div className="modal-head"><h2>{title}</h2><button className="icon-btn" onClick={onClose}><X size={19} /></button></div><div className="modal-body">{children}</div>{footer && <div className="modal-footer">{footer}</div>}</div></div>;
}

export function ConfirmModal({ open, title, message, onClose, onConfirm, confirmText = '확인' }) {
  return <Modal open={open} title={title} onClose={onClose} footer={<><button className="btn btn-ghost" onClick={onClose}>취소</button><button className="btn btn-primary" onClick={onConfirm}>{confirmText}</button></>}><p className="confirm-copy">{message}</p></Modal>;
}

export function LoadingState() { return <div className="loading-state"><span className="loader" /> 데이터를 불러오는 중입니다.</div>; }
export function EmptyState({ text }) { return <div className="empty-state">{text}</div>; }

export function PcbBoard({ boxes = true, defect = false }) {
  const traces = Array.from({ length: 12 });
  return <div className="pcb-board"><div className="pcb-grid" />{traces.map((_, i) => <span key={i} className={`trace trace-${i + 1}`} />)}<span className="chip chip-a">AOI</span><span className="chip chip-b" /><span className="chip chip-c" />{[1,2,3,4,5,6].map((n) => <span key={n} className={`component comp-${n}`} />)}{boxes && <><span className="detect-box box-a"><em>ic_chip 99%</em></span><span className={`detect-box box-b ${defect ? 'box-defect' : ''}`}><em>{defect ? 'led missing' : 'led 98%'}</em></span><span className="detect-box box-c"><em>capacitor 97%</em></span></>}</div>;
}
