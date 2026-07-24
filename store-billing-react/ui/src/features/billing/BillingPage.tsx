import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatMoney, round2 } from '../../shared/money';
import type { BillingSettings, CartLine, CatalogItem } from '../../shared/types';

export function BillingPage() {
  const { user } = useAuth();
  const storeId = user?.storeId || '';
  const searchRef = useRef<HTMLInputElement>(null);
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('All Categories');
  const [categories, setCategories] = useState<string[]>([]);
  const [items, setItems] = useState<CatalogItem[]>([]);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [settings, setSettings] = useState<BillingSettings | null>(null);
  const [billMode, setBillMode] = useState<'retail' | 'wholesale'>('retail');
  const [customerName, setCustomerName] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [discount, setDiscount] = useState(0);
  const [promo, setPromo] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [flags, setFlags] = useState({ doorDelivery: false, onlineCod: false, stitching: false });

  useEffect(() => {
    if (!storeId) return;
    void (async () => {
      const [catalog, billing] = await Promise.all([
        api<{ items: CatalogItem[]; categories: string[] }>(
          `/catalog?storeId=${encodeURIComponent(storeId)}&limit=300`,
        ),
        api<BillingSettings>(`/company-billing-settings?storeId=${encodeURIComponent(storeId)}`),
      ]);
      setItems(catalog.items);
      setCategories(['All Categories', ...catalog.categories]);
      setSettings(billing);
      setBillMode(billing.mode);
    })().catch((e: Error) => setMessage(e.message));
  }, [storeId]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'F2') {
        e.preventDefault();
        searchRef.current?.focus();
      }
      if (e.key === 'F8') {
        e.preventDefault();
        clearBill();
      }
      if (e.key === 'F12' || e.key === 'F9') {
        e.preventDefault();
        void checkout('cash');
      }
      if (e.key === 'Escape') {
        setSearch('');
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
    // Hotkeys bind once; checkout/clearBill always read latest via closures on re-render if deps empty is stale —
    // intentionally re-bind when cart length changes for checkout enablement.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cart.length, busy, storeId]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return items.filter((i) => {
      if (category !== 'All Categories' && i.category !== category) return false;
      if (!q) return true;
      return (
        i.name.toLowerCase().includes(q) ||
        i.sku.toLowerCase().includes(q) ||
        (i.barcode || '').toLowerCase().includes(q)
      );
    });
  }, [items, search, category]);

  const priceOf = (item: CatalogItem) =>
    billMode === 'wholesale' && item.wholesalePrice > 0 ? item.wholesalePrice : item.retailPrice;

  function addItem(item: CatalogItem) {
    if (item.stockQty <= 0) return;
    setCart((prev) => {
      const existing = prev.find((l) => l.sku === item.sku);
      if (existing) {
        return prev.map((l) =>
          l.sku === item.sku ? { ...l, qty: Math.min(l.qty + 1, item.stockQty) } : l,
        );
      }
      return [
        ...prev,
        {
          key: item.sku,
          sku: item.sku,
          name: item.name,
          qty: 1,
          rate: priceOf(item),
          gstPercent: item.gstPercent,
          hsn: item.hsn,
          stockQty: item.stockQty,
        },
      ];
    });
  }

  function setQty(sku: string, qty: number) {
    setCart((prev) =>
      prev
        .map((l) => (l.sku === sku ? { ...l, qty: Math.max(0, Math.min(qty, l.stockQty)) } : l))
        .filter((l) => l.qty > 0),
    );
  }

  function clearBill() {
    setCart([]);
    setDiscount(0);
    setPromo('');
    setCustomerName('');
    setCustomerPhone('');
    setMessage('');
  }

  const subtotal = round2(cart.reduce((s, l) => s + l.qty * l.rate, 0));
  const promoAmt = promo.trim().toUpperCase() === 'WELCOME10' ? round2(subtotal * 0.1) : 0;
  const afterDiscount = Math.max(0, round2(subtotal - discount - promoAmt));
  const tax = round2(
    cart.reduce((s, l) => {
      const line = l.qty * l.rate;
      const share = subtotal > 0 ? line / subtotal : 0;
      const taxable = afterDiscount * share;
      return s + (taxable * l.gstPercent) / (100 + l.gstPercent);
    }, 0),
  );
  const payable = afterDiscount;

  async function checkout(mode: 'cash' | 'card' | 'split' | 'credit') {
    if (!cart.length || busy || !storeId) return;
    setBusy(true);
    setMessage('');
    try {
      const lines = cart.map((l, idx) => ({
        lineNo: idx + 1,
        sku: l.sku,
        description: l.name,
        hsn: l.hsn,
        qty: l.qty,
        rate: l.rate,
        amount: round2(l.qty * l.rate),
        gstPercent: l.gstPercent,
      }));
      const res = await api<{ billNo: string }>('/bills', {
        method: 'POST',
        body: JSON.stringify({
          storeId,
          customerName: customerName || 'Walk-in',
          customerPhone,
          lines,
          subtotal,
          discount,
          promoCode: promo || undefined,
          promoAmount: promoAmt,
          tax,
          payable,
          paymentMode: mode,
          payments: [{ mode, amount: payable }],
          billingMode: billMode,
          flags,
          isOnlineCod: flags.onlineCod,
          isCredit: mode === 'credit',
          itemCount: cart.length,
        }),
      });
      setMessage(`Posted ${res.billNo}`);
      clearBill();
      const catalog = await api<{ items: CatalogItem[]; categories: string[] }>(
        `/catalog?storeId=${encodeURIComponent(storeId)}&limit=300`,
      );
      setItems(catalog.items);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : 'Checkout failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="billing-layout">
      <section className="catalog-pane">
        <div className="search-row">
          <input
            ref={searchRef}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Scan Barcode, SKU or Search Product Name (F2)"
          />
        </div>
        <div className="cat-pills">
          {categories.map((c) => (
            <button
              key={c}
              type="button"
              className={category === c ? 'active' : ''}
              onClick={() => setCategory(c)}
            >
              {c}
            </button>
          ))}
        </div>
        <div className="product-grid">
          {filtered.map((item) => {
            const out = item.stockQty <= 0;
            const low = !out && item.stockQty <= (item.minStock || 5);
            return (
              <article key={item.sku} className={`product-card${out ? ' out' : ''}`}>
                {low && <span className="badge-low">Low Stock: {item.stockQty}</span>}
                {out && <span className="badge-low">Out of Stock</span>}
                <div className="thumb">
                  {item.imageUrl ? <img src={item.imageUrl} alt="" /> : 'No image'}
                </div>
                <h3>{item.name}</h3>
                <div className="meta">
                  {item.sku} · Stock: {item.stockQty}
                </div>
                <div className="price-row">
                  <span className="price">{formatMoney(priceOf(item))}</span>
                  <button
                    className="add-btn"
                    type="button"
                    disabled={out}
                    onClick={() => addItem(item)}
                  >
                    +
                  </button>
                </div>
              </article>
            );
          })}
          {!filtered.length && <div className="empty">No products match your search.</div>}
        </div>
      </section>

      <aside className="bill-pane">
        <header>
          <div>
            <strong>Active Bill</strong>
            <div className="muted" style={{ fontSize: '0.8rem' }}>
              Mode: {billMode}
              {settings?.allowPerBillSwitch && (
                <>
                  {' · '}
                  <button
                    type="button"
                    className="linkish"
                    style={{ background: 'none', border: 'none', cursor: 'pointer' }}
                    onClick={() =>
                      setBillMode((m) => (m === 'retail' ? 'wholesale' : 'retail'))
                    }
                  >
                    Switch
                  </button>
                </>
              )}
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-secondary" type="button" onClick={() => searchRef.current?.focus()}>
              + Customer
            </button>
            <button className="btn btn-secondary" type="button" onClick={clearBill}>
              Clear
            </button>
          </div>
        </header>

        <div style={{ padding: '10px 14px', borderBottom: '1px solid var(--line)' }}>
          <div className="grid-2">
            <input
              placeholder="Customer name"
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
            />
            <input
              placeholder="Phone"
              value={customerPhone}
              onChange={(e) => setCustomerPhone(e.target.value)}
            />
          </div>
          <div style={{ display: 'flex', gap: 10, marginTop: 8, fontSize: '0.8rem' }}>
            {(['doorDelivery', 'onlineCod', 'stitching'] as const).map((k) => (
              <label key={k}>
                <input
                  type="checkbox"
                  checked={flags[k]}
                  onChange={(e) => setFlags((f) => ({ ...f, [k]: e.target.checked }))}
                />{' '}
                {k}
              </label>
            ))}
          </div>
        </div>

        <div className="bill-lines">
          {cart.map((l) => (
            <div className="bill-line" key={l.key}>
              <div>
                <strong>{l.name}</strong>
                <div className="sku">{l.sku}</div>
                <div className="qty-ctrl">
                  <button type="button" onClick={() => setQty(l.sku, l.qty - 1)}>
                    −
                  </button>
                  <span>{l.qty}</span>
                  <button type="button" onClick={() => setQty(l.sku, l.qty + 1)}>
                    +
                  </button>
                </div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div>{formatMoney(l.rate)}</div>
                <strong>{formatMoney(l.qty * l.rate)}</strong>
              </div>
            </div>
          ))}
          {!cart.length && <div className="empty">Scan or add products</div>}
        </div>

        <div className="totals">
          <div className="row">
            <span>Subtotal ({cart.length} items)</span>
            <span>{formatMoney(subtotal)}</span>
          </div>
          <div className="row">
            <span>Discount</span>
            <input
              style={{ width: 80 }}
              type="number"
              value={discount}
              onChange={(e) => setDiscount(Number(e.target.value) || 0)}
            />
          </div>
          <div className="row">
            <span>Promo</span>
            <input
              style={{ width: 120 }}
              placeholder="WELCOME10"
              value={promo}
              onChange={(e) => setPromo(e.target.value)}
            />
          </div>
          {promoAmt > 0 && (
            <div className="row">
              <span className="linkish">Promo: {promo.toUpperCase()}</span>
              <span>−{formatMoney(promoAmt)}</span>
            </div>
          )}
          <div className="row">
            <span>Est. GST in price</span>
            <span>{formatMoney(tax)}</span>
          </div>
        </div>

        <div className="payable">
          <span>Payable Amount</span>
          <strong>{formatMoney(payable)}</strong>
        </div>

        <div className="pay-actions">
          <button className="btn btn-secondary" type="button" disabled={busy} onClick={() => void checkout('cash')}>
            Cash
          </button>
          <button className="btn btn-primary" type="button" disabled={busy} onClick={() => void checkout('card')}>
            Card
          </button>
        </div>
        <div style={{ padding: '0 12px 12px' }}>
          <button
            className="btn btn-secondary btn-block"
            type="button"
            disabled={busy}
            onClick={() => void checkout('split')}
          >
            Split Payment
          </button>
          {message && <div style={{ marginTop: 8, fontSize: '0.85rem' }}>{message}</div>}
        </div>
      </aside>
    </div>
  );
}
