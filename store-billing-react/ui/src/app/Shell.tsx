import { useEffect, useMemo, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../shared/auth';
import { PRIMARY_ONLY_SCREENS } from '../shared/types';
import { syncHealth } from '../shared/storePos';

type NavItem = { to: string; label: string; shortcut: string; section: string; primaryOnly?: boolean };

const NAV: NavItem[] = [
  { to: '/', label: 'Billing', shortcut: 'Ctrl+A', section: 'TRANSACTIONS' },
  { to: '/vouchers', label: 'Vouchers', shortcut: 'Ctrl+V', section: 'TRANSACTIONS' },
  { to: '/quotations', label: 'Quotations', shortcut: 'Ctrl+Q', section: 'TRANSACTIONS' },
  { to: '/barcodes', label: 'Barcodes', shortcut: 'Ctrl+B', section: 'TRANSACTIONS' },
  { to: '/returns', label: 'Returns', shortcut: 'Ctrl+R', section: 'TRANSACTIONS' },
  { to: '/adjustments', label: 'Adjustments', shortcut: 'Ctrl+T', section: 'TRANSACTIONS' },
  { to: '/duplicate', label: 'Duplicate', shortcut: 'Ctrl+J', section: 'TRANSACTIONS' },
  { to: '/online-sales', label: 'Online Sales', shortcut: 'Ctrl+O', section: 'TRANSACTIONS', primaryOnly: true },
  { to: '/credit-bills', label: 'Credit Bills', shortcut: 'Ctrl+I', section: 'TRANSACTIONS', primaryOnly: true },
  { to: '/customers', label: 'Customers', shortcut: 'Ctrl+U', section: 'MASTERS' },
  { to: '/salesmen', label: 'Salesman', shortcut: 'Ctrl+M', section: 'MASTERS' },
  { to: '/dashboard', label: 'Dashboard', shortcut: 'Ctrl+D', section: 'REPORTS', primaryOnly: true },
  { to: '/analytics', label: 'Analytics', shortcut: 'Ctrl+Y', section: 'REPORTS', primaryOnly: true },
  { to: '/ledger', label: 'Ledger', shortcut: 'Ctrl+L', section: 'REPORTS', primaryOnly: true },
  { to: '/bills', label: 'Bill Lookup', shortcut: 'Ctrl+K', section: 'REPORTS' },
  { to: '/day-close', label: 'Day Close', shortcut: 'Ctrl+W', section: 'DAY END' },
  { to: '/expenses', label: 'Expenses', shortcut: 'Ctrl+E', section: 'DAY END', primaryOnly: true },
  { to: '/settings', label: 'Settings', shortcut: 'Ctrl+,', section: 'DAY END', primaryOnly: true },
];

const LABELS: Record<string, string> = Object.fromEntries(NAV.map((n) => [n.to, n.label]));
LABELS['/'] = 'Billing';

const HEADER_CHIPS = ['/', '/vouchers', '/quotations', '/barcodes', '/customers', '/returns', '/day-close', '/duplicate', '/dashboard', '/ledger'];

export function Shell() {
  const { ctx, logout, isPrimaryTill } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [sidebar, setSidebar] = useState(true);
  const [search, setSearch] = useState('');
  const [reachable, setReachable] = useState(true);
  const [helpOpen, setHelpOpen] = useState(false);

  const visibleNav = useMemo(
    () => NAV.filter((n) => !n.primaryOnly || isPrimaryTill),
    [isPrimaryTill],
  );

  useEffect(() => {
    let alive = true;
    const tick = async () => {
      try {
        await syncHealth();
        if (alive) setReachable(true);
      } catch {
        if (alive) setReachable(false);
      }
    };
    void tick();
    const id = window.setInterval(tick, 30000);
    return () => {
      alive = false;
      window.clearInterval(id);
    };
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement)?.tagName;
      const typing = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (e.target as HTMLElement)?.isContentEditable;
      if (e.key === 'F1') {
        e.preventDefault();
        setHelpOpen(true);
        return;
      }
      if (e.key === 'F12') {
        e.preventDefault();
        logout();
        navigate('/login');
        return;
      }
      if (e.ctrlKey && e.key.toLowerCase() === 'g') {
        e.preventDefault();
        setSidebar((s) => !s);
        return;
      }
      if (e.ctrlKey && e.key.toLowerCase() === 'f') {
        e.preventDefault();
        document.getElementById('global-search')?.focus();
        return;
      }
      if (!e.ctrlKey || typing) return;
      const map: Record<string, string> = {
        a: '/',
        v: '/vouchers',
        q: '/quotations',
        b: '/barcodes',
        r: '/returns',
        t: '/adjustments',
        j: '/duplicate',
        o: '/online-sales',
        i: '/credit-bills',
        u: '/customers',
        m: '/salesmen',
        d: '/dashboard',
        y: '/analytics',
        l: '/ledger',
        k: '/bills',
        w: '/day-close',
        e: '/expenses',
        ',': '/settings',
      };
      const path = map[e.key.toLowerCase()];
      if (!path) return;
      e.preventDefault();
      const screen = path.replace(/^\//, '') || 'billing';
      if (PRIMARY_ONLY_SCREENS.has(screen) && !isPrimaryTill) return;
      navigate(path);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [isPrimaryTill, logout, navigate]);

  const pageLabel = LABELS[location.pathname] || 'Billing';

  return (
    <div className={`app-shell${sidebar ? '' : ' sidebar-hidden'}`}>
      <aside className="sidebar">
        <div className="sidebar-header">
          <div className="sidebar-brand">TruBilling</div>
          <div className="sidebar-meta">{ctx?.storeId}</div>
          <div className="sidebar-hint">Sidebar on · Ctrl+G hide</div>
        </div>
        <nav className="sidebar-nav">
          {(['TRANSACTIONS', 'MASTERS', 'REPORTS', 'DAY END'] as const).map((section) => {
            const items = visibleNav.filter((n) => n.section === section);
            if (!items.length) return null;
            return (
              <div key={section}>
                <div className="nav-section">{section}</div>
                {items.map((n) => (
                  <NavLink
                    key={n.to}
                    to={n.to}
                    end={n.to === '/'}
                    className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
                  >
                    <span>{n.label}</span>
                    <span className="sc">{n.shortcut}</span>
                  </NavLink>
                ))}
              </div>
            );
          })}
        </nav>
        <div className="sidebar-footer">
          <button type="button" className="ghost-btn" onClick={() => setHelpOpen(true)}>
            ⌨ Shortcuts F1
          </button>
          <button
            type="button"
            className="ghost-btn"
            onClick={() => {
              logout();
              navigate('/login');
            }}
          >
            Sign out
          </button>
        </div>
      </aside>

      <div className="workspace">
        <header className="header-bar">
          <div className="header-top">
            <div className="header-left">
              <button type="button" className="icon-btn" title="Go To (Ctrl+G)" onClick={() => setSidebar((s) => !s)}>
                ☰
              </button>
              <div>
                <div className="page-crumb">{pageLabel}</div>
                <div className="till-line">
                  Till {ctx?.posCounter} · {ctx?.deviceId}
                </div>
              </div>
            </div>
            <div className="header-search">
              <input
                id="global-search"
                className="field"
                placeholder="Global search (Ctrl+F)"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && search.trim()) navigate(`/bills?q=${encodeURIComponent(search.trim())}`);
                }}
              />
            </div>
            <div className="header-right">
              <span className="status-chip">{reachable ? 'ONLINE' : 'UNREACHABLE'}</span>
              <div className="user-badge">{ctx?.userName || ctx?.email || 'Cashier'}</div>
            </div>
          </div>
          <div className="header-chips">
            {HEADER_CHIPS.filter((to) => {
              const item = NAV.find((n) => n.to === to);
              return !item?.primaryOnly || isPrimaryTill;
            }).map((to) => (
              <button
                key={to}
                type="button"
                className={`chip${location.pathname === to ? ' active' : ''}`}
                onClick={() => navigate(to)}
              >
                {LABELS[to]}
              </button>
            ))}
            <button type="button" className="chip" onClick={() => setSidebar(true)}>
              More…
            </button>
          </div>
        </header>

        <main className="content">
          <Outlet />
        </main>

        <footer className="footer-bar">
          <span>
            Central {reachable ? 'Online' : 'Unreachable'} · store-pos API
          </span>
          <span className="muted">
            {ctx?.storeId} / POS {ctx?.posCounter} · TruBilling React
          </span>
        </footer>
      </div>

      {helpOpen ? (
        <div className="modal-backdrop" onClick={() => setHelpOpen(false)} role="presentation">
          <div className="modal" onClick={(e) => e.stopPropagation()} role="dialog">
            <div className="modal-h">
              <span>Keyboard shortcuts</span>
              <button type="button" className="ghost-btn" style={{ width: 'auto' }} onClick={() => setHelpOpen(false)}>
                ✕
              </button>
            </div>
            <div className="modal-b">
              <p className="msg">Same Go To shortcuts as WPF MainWindow.</p>
              <ul>
                {NAV.map((n) => (
                  <li key={n.to}>
                    <strong>{n.shortcut}</strong> — {n.label}
                    {n.primaryOnly ? ' (POS 1)' : ''}
                  </li>
                ))}
                <li>
                  <strong>F9 / Ctrl+Enter</strong> — Post bill (on Billing)
                </li>
                <li>
                  <strong>F8 / Ctrl+H</strong> — Hold bill
                </li>
                <li>
                  <strong>F12</strong> — Sign out
                </li>
              </ul>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
