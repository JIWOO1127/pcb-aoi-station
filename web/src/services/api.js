import * as data from '../mocks/data';

const delay = (value, ms = 180) => new Promise((resolve) => setTimeout(() => resolve(structuredClone(value)), ms));

export const authService = { login: (credentials) => delay({ user: data.currentUser, token: `mock-${credentials.employeeId}` }) };
export const dashboardService = { getSummary: () => delay({ hourly: data.hourlyData, defects: data.defectDistribution, recent: data.inspections.filter((item) => item.result === 'FAIL').slice(0, 5) }) };
export const productService = { list: () => delay(data.products), get: (id) => delay(data.products.find((item) => item.id === id)) };
export const recipeService = { list: () => delay(data.recipes), get: (id) => delay(data.recipes.find((item) => item.id === id)) };
export const inspectionService = { list: () => delay(data.inspections), get: (id) => delay(data.inspections.find((item) => item.id === id) || data.inspections[0]) };
export const stationService = { list: () => delay(data.stations), get: (id) => delay(data.stations.find((item) => item.id === id)) };
export const modelService = { list: () => delay(data.models), get: (id) => delay(data.models.find((item) => item.id === id)) };
export const userService = { list: () => delay(data.users) };
export const auditService = { list: () => delay(data.auditLogs) };
