import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Boxes, Factory, LockKeyhole, ScanLine, UserRound } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';

export default function LoginPage() {
  const [employeeId, setEmployeeId] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const submit = async (event) => {
    event.preventDefault(); setLoading(true); setError('');
    try { await login({ employeeId, password }); navigate(location.state?.from?.pathname || '/dashboard', { replace: true }); }
    catch (exception) { setError(exception.message); }
    finally { setLoading(false); }
  };
  return <div className="login-page">
    <div className="login-visual"><div className="login-brand"><div className="brand-mark large"><Boxes size={28} /></div><div><strong>PCB AOI</strong><span>Manufacturing Execution System</span></div></div><div className="scan-illustration"><div className="scan-line" /><div className="board-outline"><span /><span /><span /><span /><b>AOI</b></div></div><div className="login-message"><p>QUALITY CONTROL PLATFORM</p><h1>생산 품질을 한눈에,<br />더 빠르고 정확하게.</h1><span>검사 결과부터 설비 현황, 불량 분석까지<br />하나의 MES에서 통합 관리합니다.</span></div><div className="visual-status"><span><Factory size={16} /> 화성 제1공장</span><span><i /> System Online</span></div></div>
    <div className="login-panel"><form className="login-card" onSubmit={submit}><div className="mobile-login-logo"><ScanLine size={24} /> PCB AOI</div><p className="eyebrow">SECURE ACCESS</p><h2>MES 로그인</h2><p className="login-help">승인된 사번과 PIN을 입력하세요.</p><label><span>사번</span><div className="input-icon"><UserRound size={18} /><input value={employeeId} onChange={(event) => setEmployeeId(event.target.value)} placeholder="사번 입력" required minLength={3} maxLength={50} autoComplete="username" /></div></label><label><span>PIN</span><div className="input-icon"><LockKeyhole size={18} /><input type="password" value={password} onChange={(event) => setPassword(event.target.value)} placeholder="PIN 입력" required minLength={4} maxLength={64} autoComplete="current-password" /></div></label>{error && <p className="login-error" role="alert">{error}</p>}<button className="login-button" disabled={loading}>{loading ? '인증 중...' : '로그인'}</button><div className="login-note"><LockKeyhole size={14} /> 사내 계정으로만 접근할 수 있습니다.</div></form><p className="login-copyright">© 2026 PCB AOI MES. All rights reserved.</p></div>
  </div>;
}
