import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Activity, BarChart3, Boxes, ChevronLeft, ChevronRight, ClipboardList, Cpu, FileClock, Gauge, LayoutDashboard, LogOut, Menu, Package, PanelLeftClose, Settings, ShieldCheck, Users, X } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import useCurrentTime from '../hooks/useCurrentTime';

const groups = [
  { items: [{ to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard }] },
  { label: '기준정보 관리', items: [{ to: '/products', label: '제품 관리', icon: Package }, { to: '/recipes', label: 'Recipe 관리', icon: ClipboardList }] },
  { label: '검사 관리', items: [{ to: '/inspections', label: '검사 이력', icon: FileClock }, { to: '/defects', label: '불량 분석', icon: BarChart3 }] },
  { label: '설비 관리', items: [{ to: '/stations', label: 'Station 현황', icon: Gauge }] },
  { label: 'AI 관리', items: [{ to: '/models', label: '모델 관리', icon: Cpu }] },
  { label: '시스템 관리', roles: ['ADMIN'], items: [{ to: '/users', label: '사용자 관리', icon: Users }, { to: '/audit-logs', label: '감사 로그', icon: ShieldCheck }] },
];

export default function AppLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const navigate = useNavigate();
  const { user: currentUser, logout } = useAuth();
  const now = useCurrentTime();
  const currentTime = now.toLocaleString('ko-KR', {
    timeZone: 'Asia/Seoul', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
  });
  return <div className={`app-shell ${collapsed ? 'sidebar-collapsed' : ''}`}>
    <aside className={`sidebar ${mobileOpen ? 'mobile-open' : ''}`}>
      <div className="brand"><div className="brand-mark"><Boxes size={22} /></div><div className="brand-copy"><strong>PCB AOI</strong><span>Manufacturing MES</span></div><button className="mobile-close" onClick={() => setMobileOpen(false)}><X size={20} /></button></div>
      <div className="plant-status"><span className="pulse" /><div><b>화성 제1공장</b><small>MES ONLINE</small></div></div>
      <nav>{groups.filter((group) => !group.roles || group.roles.includes(currentUser.role)).map((group, groupIndex) => <div className="nav-group" key={group.label || groupIndex}>{group.label && <p>{group.label}</p>}{group.items.map(({ to, label, icon: Icon }) => <NavLink to={to} key={to} onClick={() => setMobileOpen(false)} className={({ isActive }) => isActive ? 'active' : ''}><Icon size={18} /><span>{label}</span></NavLink>)}</div>)}</nav>
      <button className="collapse-button" onClick={() => setCollapsed((value) => !value)}>{collapsed ? <ChevronRight size={18} /> : <><PanelLeftClose size={18} /><span>사이드바 접기</span></>}</button>
    </aside>
    {mobileOpen && <div className="mobile-overlay" onClick={() => setMobileOpen(false)} />}
    <div className="main-shell">
      <header className="topbar"><button className="mobile-menu" onClick={() => setMobileOpen(true)}><Menu size={20} /></button><div className="system-state"><Activity size={16} /><span>System normal</span><b>5 / 6 Stations</b></div><div className="topbar-right"><div className="clock"><small>현재 시각</small><strong>{currentTime} KST</strong></div><div className="user-block"><span className="avatar">{currentUser.name.slice(0, 1)}</span><div><strong>{currentUser.name}</strong><small>{currentUser.role}</small></div></div><button className="logout" onClick={() => { logout(); navigate('/login', { replace: true }); }} title="로그아웃"><LogOut size={18} /></button></div></header>
      <main className="content"><Outlet /></main>
      <footer className="footer"><span>PCB AOI Manufacturing Execution System</span><span>MES Web v1.0.0 · LIVE API</span></footer>
    </div>
  </div>;
}
