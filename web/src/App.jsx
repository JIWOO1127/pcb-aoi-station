import { Navigate, Route, Routes } from 'react-router-dom';
import AppLayout from './layouts/AppLayout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import ProductsPage from './pages/ProductsPage';
import RecipesPage from './pages/RecipesPage';
import RecipeDetailPage from './pages/RecipeDetailPage';
import InspectionsPage from './pages/InspectionsPage';
import InspectionDetailPage from './pages/InspectionDetailPage';
import DefectsPage from './pages/DefectsPage';
import StationsPage from './pages/StationsPage';
import { ModelDetailPage, ModelsPage } from './pages/ModelsPage';
import UsersPage from './pages/UsersPage';
import AuditLogsPage from './pages/AuditLogsPage';
import ProtectedRoute from './auth/ProtectedRoute';

export default function App() {
  return <Routes><Route path="/login" element={<LoginPage />} /><Route element={<ProtectedRoute />}><Route element={<AppLayout />}><Route path="/dashboard" element={<DashboardPage />} /><Route path="/products" element={<ProductsPage />} /><Route path="/products/:productId" element={<ProductsPage />} /><Route path="/recipes" element={<RecipesPage />} /><Route path="/recipes/:recipeId" element={<RecipeDetailPage />} /><Route path="/inspections" element={<InspectionsPage />} /><Route path="/inspections/:inspectionId" element={<InspectionDetailPage />} /><Route path="/defects" element={<DefectsPage />} /><Route path="/stations" element={<StationsPage />} /><Route path="/stations/:stationId" element={<StationsPage />} /><Route path="/models" element={<ModelsPage />} /><Route path="/models/:modelId" element={<ModelDetailPage />} /><Route element={<ProtectedRoute roles={['ADMIN']} />}><Route path="/users" element={<UsersPage />} /><Route path="/audit-logs" element={<AuditLogsPage />} /></Route></Route></Route><Route path="*" element={<Navigate to="/dashboard" replace />} /></Routes>;
}
