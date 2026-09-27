export const currentUser = { employeeId: 'A-1042', name: '김민준', role: 'ADMIN' };

export const hourlyData = [
  { time: '08', inspections: 118, defectRate: 2.5 }, { time: '09', inspections: 184, defectRate: 3.1 },
  { time: '10', inspections: 212, defectRate: 2.2 }, { time: '11', inspections: 198, defectRate: 4.0 },
  { time: '12', inspections: 92, defectRate: 3.3 }, { time: '13', inspections: 206, defectRate: 2.7 },
  { time: '14', inspections: 228, defectRate: 3.8 }, { time: '15', inspections: 196, defectRate: 2.9 },
];

export const defectDistribution = [
  { name: '부품 누락', value: 41, key: 'MISSING_COMPONENT' },
  { name: '위치 불량', value: 28, key: 'WRONG_POSITION' },
  { name: '오부품', value: 19, key: 'WRONG_COMPONENT' },
  { name: '전선 색상', value: 12, key: 'WRONG_WIRE_COLOR' },
];

export const products = [
  { id: 'PCB-A100', name: 'Main Control Board', description: '주제어 장치용 PCB', recipe: 'v3.2', createdAt: '2026-08-12', status: 'ACTIVE' },
  { id: 'PCB-B220', name: 'Sensor Interface', description: '센서 인터페이스 모듈', recipe: 'v2.1', createdAt: '2026-08-18', status: 'ACTIVE' },
  { id: 'PCB-C310', name: 'Power Module', description: '24V 전원 제어 모듈', recipe: 'v1.8', createdAt: '2026-08-22', status: 'ACTIVE' },
  { id: 'PCB-D410', name: 'I/O Expansion', description: '산업용 입출력 확장 보드', recipe: 'v1.4', createdAt: '2026-09-01', status: 'INACTIVE' },
  { id: 'PCB-E500', name: 'Display Driver', description: 'LCD 디스플레이 구동 보드', recipe: 'v2.0', createdAt: '2026-09-05', status: 'ACTIVE' },
  { id: 'PCB-F610', name: 'Safety Relay', description: '안전 릴레이 제어 보드', recipe: 'v1.1', createdAt: '2026-09-14', status: 'ACTIVE' },
];

export const recipes = [
  { id: 'RCP-032', product: 'PCB-A100', name: 'Main Board Standard', version: 'v3.2', status: 'ACTIVE', createdBy: '이서연', createdAt: '2026-09-21 14:20' },
  { id: 'RCP-033', product: 'PCB-A100', name: 'Main Board Standard', version: 'v3.3', status: 'DRAFT', createdBy: '이서연', createdAt: '2026-09-26 09:15' },
  { id: 'RCP-021', product: 'PCB-B220', name: 'Sensor Interface', version: 'v2.1', status: 'ACTIVE', createdBy: '박정호', createdAt: '2026-09-18 11:02' },
  { id: 'RCP-018', product: 'PCB-C310', name: 'Power Module 24V', version: 'v1.8', status: 'ACTIVE', createdBy: '김민준', createdAt: '2026-09-12 16:44' },
  { id: 'RCP-014', product: 'PCB-D410', name: 'I/O Expansion', version: 'v1.4', status: 'INACTIVE', createdBy: '최유진', createdAt: '2026-08-30 10:18' },
];

export const inspections = [
  { id: 'INS-260927-0842', time: '2026-09-27 15:42:18', product: 'PCB-A100', station: 'ST-01', recipe: 'v3.2', model: 'RTMDet-m v2.4', result: 'FAIL', defect: 'MISSING_COMPONENT' },
  { id: 'INS-260927-0841', time: '2026-09-27 15:41:52', product: 'PCB-A100', station: 'ST-01', recipe: 'v3.2', model: 'RTMDet-m v2.4', result: 'PASS', defect: '-' },
  { id: 'INS-260927-0840', time: '2026-09-27 15:41:17', product: 'PCB-B220', station: 'ST-03', recipe: 'v2.1', model: 'RTMDet-m v2.4', result: 'FAIL', defect: 'WRONG_POSITION' },
  { id: 'INS-260927-0839', time: '2026-09-27 15:40:46', product: 'PCB-C310', station: 'ST-02', recipe: 'v1.8', model: 'RTMDet-m v2.3', result: 'PASS', defect: '-' },
  { id: 'INS-260927-0838', time: '2026-09-27 15:40:03', product: 'PCB-E500', station: 'ST-04', recipe: 'v2.0', model: 'RTMDet-m v2.4', result: 'FAIL', defect: 'WRONG_COMPONENT' },
  { id: 'INS-260927-0837', time: '2026-09-27 15:39:21', product: 'PCB-A100', station: 'ST-01', recipe: 'v3.2', model: 'RTMDet-m v2.4', result: 'PASS', defect: '-' },
  { id: 'INS-260927-0836', time: '2026-09-27 15:38:57', product: 'PCB-F610', station: 'ST-05', recipe: 'v1.1', model: 'RTMDet-m v2.4', result: 'FAIL', defect: 'WRONG_WIRE_COLOR' },
  { id: 'INS-260927-0835', time: '2026-09-27 15:38:12', product: 'PCB-B220', station: 'ST-03', recipe: 'v2.1', model: 'RTMDet-m v2.4', result: 'PASS', defect: '-' },
];

export const stations = [
  { id: 'ST-01', name: '조립 1라인', status: 'RUNNING', product: 'PCB-A100', recipe: 'v3.2', camera: 'ONLINE', ai: 'ONLINE', mes: 'ONLINE', lastSeen: '3초 전', today: 428 },
  { id: 'ST-02', name: '조립 2라인', status: 'RUNNING', product: 'PCB-C310', recipe: 'v1.8', camera: 'ONLINE', ai: 'ONLINE', mes: 'ONLINE', lastSeen: '5초 전', today: 391 },
  { id: 'ST-03', name: '검증 라인', status: 'ERROR', product: 'PCB-B220', recipe: 'v2.1', camera: 'ERROR', ai: 'ONLINE', mes: 'ONLINE', lastSeen: '42초 전', today: 307 },
  { id: 'ST-04', name: '소형 PCB 라인', status: 'STOP', product: 'PCB-E500', recipe: 'v2.0', camera: 'ONLINE', ai: 'ONLINE', mes: 'ONLINE', lastSeen: '1분 전', today: 216 },
  { id: 'ST-05', name: '안전모듈 라인', status: 'RUNNING', product: 'PCB-F610', recipe: 'v1.1', camera: 'ONLINE', ai: 'ONLINE', mes: 'ONLINE', lastSeen: '8초 전', today: 144 },
  { id: 'ST-06', name: '예비 라인', status: 'OFFLINE', product: '-', recipe: '-', camera: 'OFFLINE', ai: 'OFFLINE', mes: 'OFFLINE', lastSeen: '2시간 전', today: 0 },
];

export const models = [
  { id: 'MDL-024', name: 'RTMDet-m', version: 'v2.4', framework: 'MMDetection', classes: 8, input: '640 × 640', map50: 0.993, map95: 0.744, status: 'ACTIVE', createdAt: '2026-09-20', note: '생산 배포 모델' },
  { id: 'MDL-023', name: 'RTMDet-m', version: 'v2.3', framework: 'MMDetection', classes: 8, input: '640 × 640', map50: 0.988, map95: 0.731, status: 'INACTIVE', createdAt: '2026-08-28', note: '오부품 데이터 보강' },
  { id: 'MDL-019', name: 'YOLOv11-m', version: 'v1.9', framework: 'Ultralytics', classes: 8, input: '640 × 640', map50: 0.976, map95: 0.702, status: 'INACTIVE', createdAt: '2026-07-14', note: '비교 평가용' },
];

export const users = [
  { employeeId: 'A-1042', name: '김민준', role: 'ADMIN', status: 'ACTIVE', lastLogin: '2026-09-27 14:03' },
  { employeeId: 'E-0811', name: '이서연', role: 'ENGINEER', status: 'ACTIVE', lastLogin: '2026-09-27 13:42' },
  { employeeId: 'E-0732', name: '박정호', role: 'ENGINEER', status: 'ACTIVE', lastLogin: '2026-09-26 17:12' },
  { employeeId: 'V-0204', name: '최유진', role: 'VIEWER', status: 'ACTIVE', lastLogin: '2026-09-27 09:28' },
  { employeeId: 'V-0188', name: '정하늘', role: 'VIEWER', status: 'INACTIVE', lastLogin: '2026-08-31 10:05' },
];

export const auditLogs = [
  { time: '2026-09-27 15:18:22', user: '김민준 (A-1042)', action: 'RECIPE_ACTIVATE', target: 'RCP-032 / v3.2', before: 'DRAFT', after: 'ACTIVE' },
  { time: '2026-09-27 14:03:11', user: '김민준 (A-1042)', action: 'LOGIN', target: 'MES Web', before: '-', after: 'SUCCESS' },
  { time: '2026-09-27 13:51:45', user: '이서연 (E-0811)', action: 'RECIPE_UPDATE', target: 'RCP-033 / v3.3', before: 'Tolerance 2.0', after: 'Tolerance 1.5' },
  { time: '2026-09-27 11:24:08', user: '박정호 (E-0732)', action: 'PRODUCT_UPDATE', target: 'PCB-B220', before: '설명 없음', after: '센서 인터페이스 모듈' },
  { time: '2026-09-26 16:42:37', user: '김민준 (A-1042)', action: 'MODEL_ACTIVATE', target: 'RTMDet-m v2.4', before: 'INACTIVE', after: 'ACTIVE' },
  { time: '2026-09-26 10:18:15', user: '김민준 (A-1042)', action: 'USER_UPDATE', target: 'V-0188', before: 'ACTIVE', after: 'INACTIVE' },
];

export const components = [
  { className: 'ic_chip', count: 2, position: '(182, 96)', tolerance: '± 2.0 mm', box: [23, 25, 18, 22] },
  { className: 'led', count: 2, position: '(356, 104)', tolerance: '± 1.5 mm', box: [58, 24, 9, 12] },
  { className: 'capacitor', count: 4, position: '(214, 226)', tolerance: '± 2.0 mm', box: [31, 58, 11, 18] },
  { className: 'resistance330', count: 3, position: '(402, 218)', tolerance: '± 2.5 mm', box: [65, 55, 17, 8] },
  { className: 'red_wire', count: 1, position: '(92, 284)', tolerance: '± 3.0 mm', box: [10, 71, 20, 8] },
];

export const dailyDefects = [
  { date: '09/21', rate: 3.4 }, { date: '09/22', rate: 2.8 }, { date: '09/23', rate: 3.9 },
  { date: '09/24', rate: 2.6 }, { date: '09/25', rate: 3.1 }, { date: '09/26', rate: 2.4 }, { date: '09/27', rate: 3.2 },
];

export const productDefects = [
  { name: 'PCB-A100', rate: 3.8 }, { name: 'PCB-B220', rate: 4.6 }, { name: 'PCB-C310', rate: 2.1 },
  { name: 'PCB-E500', rate: 3.4 }, { name: 'PCB-F610', rate: 2.8 },
];
