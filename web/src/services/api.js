import * as data from '../mocks/data';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080/api';
let unauthorizedHandler = () => {};
export const setUnauthorizedHandler = (handler) => { unauthorizedHandler = handler; };

async function request(path, options = {}) {
  const stored = JSON.parse(sessionStorage.getItem('pcb-aoi-auth') || 'null');
  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...(stored?.accessToken ? { Authorization: `Bearer ${stored.accessToken}` } : {}), ...options.headers } });
  if (response.status === 401) unauthorizedHandler();
  const body = await response.text();
  const contentType = response.headers.get('content-type') || '';
  const payload = body && contentType.includes('application/json') ? JSON.parse(body) : body || null;
  if (!response.ok) {
    throw new Error(payload?.message || '서버 요청에 실패했습니다.');
  }
  return payload;
}

const delay = (value, ms = 180) => new Promise((resolve) => setTimeout(() => resolve(structuredClone(value)), ms));

export const authService = { login: ({ employeeId, password }) => request('/auth/login', { method: 'POST', body: JSON.stringify({ employeeId, pin: password }) }), me: () => request('/auth/me') };
export const dashboardService = { getSummary: () => delay({ hourly: data.hourlyData, defects: data.defectDistribution, recent: data.inspections.filter((item) => item.result === 'FAIL').slice(0, 5) }) };
export const productService = { list: () => delay(data.products), get: (id) => delay(data.products.find((item) => item.id === id)) };
export const recipeService = {
  list: () => request('/recipes'),
  get: (id) => request(`/recipes/${id}`),
};
export const inspectionService = {
  list: (filters = {}) => request(`/inspections?${new URLSearchParams(Object.entries(filters).filter(([, value]) => value)).toString()}`),
  get: (id) => request(`/inspections/${id}`),
};
export const stationService = { list: () => delay(data.stations), get: (id) => delay(data.stations.find((item) => item.id === id)) };
export const modelService = { list: () => delay(data.models), get: (id) => delay(data.models.find((item) => item.id === id)) };
export const userService = {
  list: () => request('/admin/users'),
  create: ({ employeeId, name, password, role, active }) => request('/admin/users', { method: 'POST', body: JSON.stringify({ employeeId, name, password, role, active }) }),
  update: (employeeId, user) => request(`/admin/users/${employeeId}`, { method: 'PUT', body: JSON.stringify(user) }),
  resetPassword: (employeeId, password) => request(`/admin/users/${employeeId}/reset-password`, { method: 'POST', body: JSON.stringify({ password }) }),
};
export const auditService = { list: (filters = {}) => request(`/admin/audit-logs?${new URLSearchParams(Object.entries(filters).filter(([, value]) => value)).toString()}`) };
