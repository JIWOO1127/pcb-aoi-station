import * as data from '../mocks/data';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080/api';
let unauthorizedHandler = () => {};
export const setUnauthorizedHandler = (handler) => { unauthorizedHandler = handler; };

async function request(path, options = {}) {
  const stored = JSON.parse(sessionStorage.getItem('pcb-aoi-auth') || 'null');
  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...(stored?.accessToken ? { Authorization: `Bearer ${stored.accessToken}` } : {}), ...options.headers } });
  if (response.status === 401) unauthorizedHandler();
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || '서버 요청에 실패했습니다.');
  }
  return response.json();
}

const delay = (value, ms = 180) => new Promise((resolve) => setTimeout(() => resolve(structuredClone(value)), ms));

export const authService = { login: ({ employeeId, password }) => request('/auth/login', { method: 'POST', body: JSON.stringify({ employeeId, pin: password }) }), me: () => request('/auth/me') };
export const dashboardService = { getSummary: () => delay({ hourly: data.hourlyData, defects: data.defectDistribution, recent: data.inspections.filter((item) => item.result === 'FAIL').slice(0, 5) }) };
export const productService = { list: () => delay(data.products), get: (id) => delay(data.products.find((item) => item.id === id)) };
export const recipeService = { list: () => delay(data.recipes), get: (id) => delay(data.recipes.find((item) => item.id === id)) };
export const inspectionService = { list: () => delay(data.inspections), get: (id) => delay(data.inspections.find((item) => item.id === id) || data.inspections[0]) };
export const stationService = { list: () => delay(data.stations), get: (id) => delay(data.stations.find((item) => item.id === id)) };
export const modelService = { list: () => delay(data.models), get: (id) => delay(data.models.find((item) => item.id === id)) };
export const userService = {
  list: () => request('/admin/users'),
  create: (user) => request('/admin/users', { method: 'POST', body: JSON.stringify(user) }),
  update: (employeeId, user) => request(`/admin/users/${employeeId}`, { method: 'PUT', body: JSON.stringify(user) }),
  resetPassword: (employeeId, password) => request(`/admin/users/${employeeId}/reset-password`, { method: 'POST', body: JSON.stringify({ password }) }),
};
export const auditService = { list: (filters = {}) => request(`/admin/audit-logs?${new URLSearchParams(Object.entries(filters).filter(([, value]) => value)).toString()}`) };
