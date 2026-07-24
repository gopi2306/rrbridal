import { useEffect, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney } from '../../shared/money';
import type { CatalogItem } from '../../shared/types';

type InventoryResponse = {
  items: CatalogItem[];
  page: number;
  limit: number;
  total: number;
  brands: string[];
  categories: string[];
  kpis: { totalItems: number; lowStock: number; categories: number; totalValue: number };
};

export function InventoryPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('All Categories');
  const [brand, setBrand] = useState('All Brands');
  const [stockLevel, setStockLevel] = useState<'all' | 'low' | 'out'>('all');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<InventoryResponse | null>(null);
  const [error, setError] = useState('');

  async function load(p = page) {
    if (!storeId) return;
    const qs = new URLSearchParams({
      storeId,
      page: String(p),
      limit: '10',
    });
    if (search.trim()) qs.set('search', search.trim());
    if (category !== 'All Categories') qs.set('category', category);
    if (brand !== 'All Brands') qs.set('brand', brand);
    if (stockLevel !== 'all') qs.set('stockLevel', stockLevel);
    try {
      setData(await api<InventoryResponse>(`/inventory?${qs}`));
      setError('');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load');
    }
  }

  useEffect(() => {
    void load(1);
    setPage(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [storeId, category, brand, stockLevel]);

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.limit)) : 1;

  return (
    <div>
      <div className="page-header">
        <div>
          <div className="crumb">Management &gt; Inventory</div>
          <h1>Master Inventory</h1>
          <p>Manage your warehouse stock and retail pricing across all terminals.</p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-secondary" type="button" disabled>
            Bulk Import CSV
          </button>
          <button className="btn btn-primary" type="button" disabled>
            + New Product
          </button>
        </div>
      </div>

      {data && (
        <div className="kpi-grid">
          <div className="kpi-card">
            <label>Total Items</label>
            <strong>{data.kpis.totalItems.toLocaleString()}</strong>
          </div>
          <div className="kpi-card warn">
            <label>Low Stock</label>
            <strong>{data.kpis.lowStock}</strong>
          </div>
          <div className="kpi-card">
            <label>Categories</label>
            <strong>{data.kpis.categories}</strong>
          </div>
          <div className="kpi-card">
            <label>Total Value</label>
            <strong>{formatMoney(data.kpis.totalValue)}</strong>
          </div>
        </div>
      )}

      <div className="toolbar">
        <input
          style={{ flex: 1, minWidth: 180 }}
          placeholder="Search by SKU or Name..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && void load(1)}
        />
        <select value={category} onChange={(e) => setCategory(e.target.value)}>
          <option>All Categories</option>
          {(data?.categories || []).map((c) => (
            <option key={c}>{c}</option>
          ))}
        </select>
        <select value={brand} onChange={(e) => setBrand(e.target.value)}>
          <option>All Brands</option>
          {(data?.brands || []).map((b) => (
            <option key={b}>{b}</option>
          ))}
        </select>
        <div className="seg">
          {(['all', 'low', 'out'] as const).map((s) => (
            <button
              key={s}
              type="button"
              className={stockLevel === s ? 'active' : ''}
              onClick={() => setStockLevel(s)}
            >
              {s === 'all' ? 'All' : s === 'low' ? 'Low' : 'Out'}
            </button>
          ))}
        </div>
        <button className="btn btn-secondary" type="button" onClick={() => void load(page)}>
          ↻
        </button>
      </div>

      {error && <div className="error">{error}</div>}

      <div className="panel">
        <table className="table">
          <thead>
            <tr>
              <th>SKU</th>
              <th>Product Name</th>
              <th>Category</th>
              <th>Stock Level</th>
              <th>Unit Price</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {(data?.items || []).map((row) => {
              const max = Math.max(row.minStock * 4, 20, row.stockQty);
              const pct = Math.min(100, (row.stockQty / max) * 100);
              const low = row.stockQty > 0 && row.stockQty <= (row.minStock || 5);
              return (
                <tr key={row.sku}>
                  <td className="linkish">#{row.sku}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                      <div
                        style={{
                          width: 36,
                          height: 36,
                          borderRadius: 8,
                          background: '#e2e8f0',
                          overflow: 'hidden',
                        }}
                      >
                        {row.imageUrl && (
                          <img src={row.imageUrl} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                        )}
                      </div>
                      {row.name}
                    </div>
                  </td>
                  <td>{row.category || '—'}</td>
                  <td>
                    <div className={`stock-bar${low ? ' low' : ''}`}>
                      <div className="track">
                        <div className="fill" style={{ width: `${pct}%` }} />
                      </div>
                      <span>{row.stockQty}</span>
                    </div>
                  </td>
                  <td>{formatMoney(row.retailPrice)}</td>
                  <td>
                    <button className="btn btn-secondary" type="button" style={{ padding: '4px 8px' }}>
                      ✎
                    </button>{' '}
                    <button className="btn btn-secondary" type="button" style={{ padding: '4px 8px' }}>
                      👁
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        <div className="pagination">
          <span>
            Showing {data ? (page - 1) * data.limit + 1 : 0}-
            {data ? Math.min(page * data.limit, data.total) : 0} of {data?.total ?? 0} products
          </span>
          <div style={{ display: 'flex', gap: 6 }}>
            <button
              className="btn btn-secondary"
              type="button"
              disabled={page <= 1}
              onClick={() => {
                const p = page - 1;
                setPage(p);
                void load(p);
              }}
            >
              ‹
            </button>
            <span>
              {page} / {totalPages}
            </span>
            <button
              className="btn btn-secondary"
              type="button"
              disabled={page >= totalPages}
              onClick={() => {
                const p = page + 1;
                setPage(p);
                void load(p);
              }}
            >
              ›
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
