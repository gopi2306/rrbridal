import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../shared/auth';

const PRIMARY_ONLY = new Set([
  'dashboard',
  'analytics',
  'inventory',
  'online-sales',
  'credit-bills',
  'ledger',
  'expenses',
  'settings',
  'sync',
]);

const NAV: { to: string; label: string; icon: string; primaryOnly?: boolean }[] = [
  { to: '/', label: 'Billing', icon: '▣' },
  { to: '/inventory', label: 'Inventory', icon: '▤', primaryOnly: true },
  { to: '/sync', label: 'Sync Center', icon: '↻', primaryOnly: true },
  { to: '/history', label: 'History', icon: '◷' },
  { to: '/quotations', label: 'Quotations', icon: '☰' },
  { to: '/barcodes', label: 'Barcodes', icon: '▥' },
  { to: '/dashboard', label: 'Dashboard', icon: '◈', primaryOnly: true },
  { to: '/analytics', label: 'Analytics', icon: '↗', primaryOnly: true },
  { to: '/online-sales', label: 'Online Sales', icon: '☁', primaryOnly: true },
  { to: '/credit-bills', label: 'Credit Bills', icon: '💳', primaryOnly: true },
  { to: '/customers', label: 'Customers', icon: '☺' },
  { to: '/salesmen', label: 'Salesman', icon: '★' },
  { to: '/ledger', label: 'Ledger', icon: '≡', primaryOnly: true },
  { to: '/returns', label: 'Returns', icon: '↩' },
  { to: '/day-close', label: 'Day Close', icon: '⏱' },
  { to: '/duplicate', label: 'Duplicate', icon: '⧉' },
  { to: '/adjustments', label: 'Adjustments', icon: '✎' },
  { to: '/expenses', label: 'Expenses', icon: '₹', primaryOnly: true },
  { to: '/settings', label: 'Settings', icon: '⚙', primaryOnly: true },
];

export function Shell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const isPrimary = user?.isPrimaryTill !== false;

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          Tru<span>Billing</span>
        </div>
        <nav className="top-tabs">
          <NavLink to="/" end>
            Billing
          </NavLink>
          {isPrimary && (
            <NavLink to="/inventory">Inventory</NavLink>
          )}
          {isPrimary && <NavLink to="/sync">Sync Center</NavLink>}
          <NavLink to="/history">History</NavLink>
        </nav>
        <div className="global-search">
          <input
            placeholder="Global Search (Ctrl+K)"
            onKeyDown={(e) => {
              if (e.key === 'Enter') navigate(`/history?q=${encodeURIComponent(e.currentTarget.value)}`);
            }}
          />
        </div>
        <div className="top-actions">
          <span className="status-pill">ONLINE</span>
          <button className="btn btn-secondary" type="button" onClick={() => navigate('/sync')} title="Refresh">
            ↻
          </button>
          <button className="btn btn-secondary" type="button" onClick={() => navigate('/settings')}>
            ⚙
          </button>
          <div className="avatar">{(user?.name || user?.email || 'U').slice(0, 2).toUpperCase()}</div>
        </div>
      </header>

      <aside className="sidebar">
        <div className="terminal-card">
          <strong>Terminal {(user?.posCounter || '01').padStart(2, '0')}</strong>
          <span>User: {user?.name || user?.email || 'Cashier'}</span>
        </div>
        <nav className="nav-list">
          {NAV.map((item) => {
            if (item.primaryOnly && !isPrimary) return null;
            const locked = item.primaryOnly && !isPrimary;
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  [isActive ? 'active' : '', locked || (PRIMARY_ONLY.has(item.to.slice(1)) && !isPrimary) ? 'secondary' : '']
                    .filter(Boolean)
                    .join(' ')
                }
              >
                <span>{item.icon}</span>
                <span className="label">{item.label}</span>
              </NavLink>
            );
          })}
        </nav>
        <div className="sidebar-foot">
          <button className="btn btn-primary btn-block" type="button" onClick={() => navigate('/')}>
            Quick Checkout <span className="extra">F12</span>
          </button>
          <button className="btn btn-ghost" type="button">
            ? Help
          </button>
          <button className="btn btn-danger" type="button" onClick={() => void logout()}>
            Logout
          </button>
        </div>
      </aside>

      <main className="main">
        <Outlet />
      </main>

      <footer className="footer-bar">
        <div className="hotkeys">
          <span>F12 CHECKOUT</span>
          <span>F8 NEW BILL</span>
          <span>F2 SEARCH</span>
          <span>ESC CLEAR</span>
        </div>
        <div>
          v1.0.0-online · Store {user?.storeId} · DB CONNECTED
        </div>
      </footer>
    </div>
  );
}
