const API_BASE_URL = import.meta.env.VITE_API_BASE_URL
  || `http://${window.location.hostname || 'localhost'}:${__MES_API_PORT__}/api`;
let unauthorizedHandler = () => {};
export const setUnauthorizedHandler = (handler) => { unauthorizedHandler = handler; };

async function request(path, options = {}) {
  const stored = JSON.parse(sessionStorage.getItem('pcb-aoi-auth') || 'null');
  const isFormData = options.body instanceof FormData;
  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers: { ...(!isFormData ? { 'Content-Type': 'application/json' } : {}), ...(stored?.accessToken ? { Authorization: `Bearer ${stored.accessToken}` } : {}), ...options.headers } });
  if (response.status === 401) unauthorizedHandler();
  const body = await response.text();
  const contentType = response.headers.get('content-type') || '';
  const payload = body && contentType.includes('application/json') ? JSON.parse(body) : body || null;
  if (!response.ok) {
    throw new Error(payload?.message || '서버 요청에 실패했습니다.');
  }
  return payload;
}

export const authService = { login: ({ employeeId, password }) => request('/auth/login', { method: 'POST', body: JSON.stringify({ employeeId, pin: password }) }), me: () => request('/auth/me') };
export const dashboardService = { getSummary: () => request('/dashboard') };
export const defectService = { getAnalytics: (filters = {}) => request(`/analytics/defects?${new URLSearchParams(Object.entries(filters).filter(([, value]) => value)).toString()}`) };
export const productService = {
  list: () => request('/products'),
  get: (id) => request(`/products/${id}`),
  create: (product) => request('/products', { method: 'POST', body: JSON.stringify(product) }),
  update: (id, product) => request(`/products/${id}`, { method: 'PUT', body: JSON.stringify(product) }),
};
export const recipeService = {
  list: () => request('/recipes'),
  get: (id) => request(`/recipes/${id}`),
  create: (recipe) => request('/recipes', { method: 'POST', body: JSON.stringify(recipe) }),
};
export const recipeDraftService = {
  analyze: ({ productId, image }) => {
    const body = new FormData();
    body.append('productId', productId);
    body.append('image', image);
    return request('/recipe-drafts', { method: 'POST', body });
  },
  save: (draft) => request('/recipe-drafts', { method: 'POST', body: JSON.stringify(draft) }),
  approve: (recipeId) => request(`/recipes/${recipeId}/approve`, { method: 'POST' }),
};
export const inspectionService = {
  list: (filters = {}) => request(`/inspections?${new URLSearchParams(Object.entries(filters).filter(([, value]) => value)).toString()}`),
  get: (id) => request(`/inspections/${id}`),
};
export const stationService = { list: () => request('/stations'), get: (id) => request(`/stations/${id}`) };
export const modelService = {
  list: () => request('/models'),
  get: (id) => request(`/models/${id}`),
  create: (model) => request('/models', { method: 'POST', body: JSON.stringify(model) }),
  activate: (id) => request(`/models/${id}/activate`, { method: 'POST' }),
};
export const userService = {
  list: () => request('/admin/users'),
  create: ({ employeeId, name, password, role, active }) => request('/admin/users', { method: 'POST', body: JSON.stringify({ employeeId, name, password, role, active }) }),
  update: (employeeId, user) => request(`/admin/users/${employeeId}`, { method: 'PUT', body: JSON.stringify(user) }),
  resetPassword: (employeeId, password) => request(`/admin/users/${employeeId}/reset-password`, { method: 'POST', body: JSON.stringify({ password }) }),
};
export const auditService = { list: (filters = {}) => request(`/admin/audit-logs?${new URLSearchParams(Object.entries(filters).filter(([, value]) => value)).toString()}`) };
