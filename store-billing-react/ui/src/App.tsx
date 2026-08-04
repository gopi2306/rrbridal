import type { ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { Shell } from './app/Shell';
import { AdjustmentsPage } from './features/adjustments/AdjustmentsPage';
import { AnalyticsPage } from './features/analytics/AnalyticsPage';
import { BarcodesPage } from './features/barcodes/BarcodesPage';
import { BillingPage } from './features/billing/BillingPage';
import { BillLookupPage } from './features/bill-lookup/BillLookupPage';
import { CreditBillsPage } from './features/credit-bills/CreditBillsPage';
import { CustomersPage } from './features/customers/CustomersPage';
import { DashboardPage } from './features/dashboard/DashboardPage';
import { DayClosePage } from './features/day-close/DayClosePage';
import { DuplicatePage } from './features/duplicate/DuplicatePage';
import { ExpensesPage } from './features/expenses/ExpensesPage';
import { LedgerPage } from './features/ledger/LedgerPage';
import { LoginPage } from './features/login/LoginPage';
import { OnlineSalesPage } from './features/online-sales/OnlineSalesPage';
import { QuotationsPage } from './features/quotations/QuotationsPage';
import { ReturnsPage } from './features/returns/ReturnsPage';
import { SalesmenPage } from './features/salesmen/SalesmenPage';
import { SettingsPage } from './features/settings/SettingsPage';
import { VouchersPage } from './features/vouchers/VouchersPage';
import { useAuth } from './shared/auth';

function RequireAuth({ children }: { children: ReactNode }) {
  const { ctx, loading } = useAuth();
  if (loading) return <div className="login-page">Loading…</div>;
  if (!ctx) return <Navigate to="/login" replace />;
  return children;
}

function RequirePrimary({ children }: { children: ReactNode }) {
  const { isPrimaryTill } = useAuth();
  if (!isPrimaryTill) return <Navigate to="/" replace />;
  return children;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireAuth>
            <Shell />
          </RequireAuth>
        }
      >
        <Route index element={<BillingPage />} />
        <Route path="vouchers" element={<VouchersPage />} />
        <Route path="quotations" element={<QuotationsPage />} />
        <Route path="barcodes" element={<BarcodesPage />} />
        <Route path="returns" element={<ReturnsPage />} />
        <Route path="adjustments" element={<AdjustmentsPage />} />
        <Route path="duplicate" element={<DuplicatePage />} />
        <Route
          path="online-sales"
          element={
            <RequirePrimary>
              <OnlineSalesPage />
            </RequirePrimary>
          }
        />
        <Route
          path="credit-bills"
          element={
            <RequirePrimary>
              <CreditBillsPage />
            </RequirePrimary>
          }
        />
        <Route path="customers" element={<CustomersPage />} />
        <Route path="salesmen" element={<SalesmenPage />} />
        <Route
          path="dashboard"
          element={
            <RequirePrimary>
              <DashboardPage />
            </RequirePrimary>
          }
        />
        <Route
          path="analytics"
          element={
            <RequirePrimary>
              <AnalyticsPage />
            </RequirePrimary>
          }
        />
        <Route
          path="ledger"
          element={
            <RequirePrimary>
              <LedgerPage />
            </RequirePrimary>
          }
        />
        <Route path="bills" element={<BillLookupPage />} />
        <Route path="day-close" element={<DayClosePage />} />
        <Route
          path="expenses"
          element={
            <RequirePrimary>
              <ExpensesPage />
            </RequirePrimary>
          }
        />
        <Route
          path="settings"
          element={
            <RequirePrimary>
              <SettingsPage />
            </RequirePrimary>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
