import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError } from '../../shared/api';
import { useAuth } from '../../shared/auth';
import { formatRupee, round2, todayIso } from '../../shared/money';
import type { BillLine, CatalogProduct, CustomerRow, SalesmanRow } from '../../shared/types';
import * as storePos from '../../shared/storePos';
import { Modal } from '../dialogs/Modal';

function lineAmount(l: Omit<BillLine, 'amount' | 'lineId'> & { lineId?: string }) {
  const base = l.qty * l.rate;
  const afterDisc = base * (1 - (l.discountPercent || 0) / 100);
  return round2(afterDisc);
}

export function BillingPage() {
  const { ctx } = useAuth();
  const [holdBills, setHoldBills] = useState(false);
  const [doorDelivery, setDoorDelivery] = useState(false);
  const [onlineCod, setOnlineCod] = useState(false);
  const [stitching, setStitching] = useState(false);
  const [printInvoice, setPrintInvoice] = useState(true);
  const [deliveryDate, setDeliveryDate] = useState('');
  const [customerCode, setCustomerCode] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [salesmen, setSalesmen] = useState<SalesmanRow[]>([]);
  const [salesmanId, setSalesmanId] = useState('');
  const [skuQuery, setSkuQuery] = useState('');
  const [lines, setLines] = useState<BillLine[]>([]);
  const [status, setStatus] = useState('Ready.');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [dayOpen, setDayOpen] = useState<boolean | null>(null);

  const [productOpen, setProductOpen] = useState(false);
  const [products, setProducts] = useState<CatalogProduct[]>([]);
  const [customerOpen, setCustomerOpen] = useState(false);
  const [customers, setCustomers] = useState<CustomerRow[]>([]);
  const [customerQ, setCustomerQ] = useState('');
  const [heldOpen, setHeldOpen] = useState(false);
  const [held, setHeld] = useState<Record<string, unknown>[]>([]);
  const [payOpen, setPayOpen] = useState(false);
  const [cash, setCash] = useState('');
  const [card, setCard] = useState('');
  const [upi, setUpi] = useState('');
  const [billOnCredit, setBillOnCredit] = useState(false);

  const totals = useMemo(() => {
    const sub = round2(lines.reduce((s, l) => s + l.amount, 0));
    const tax = round2(
      lines.reduce((s, l) => {
        const taxable = l.amount / (1 + (l.gstPercent || 0) / 100);
        return s + (l.amount - taxable);
      }, 0),
    );
    return { sub, tax, payable: sub };
  }, [lines]);

  const refreshDay = useCallback(async () => {
    try {
      const session = await storePos.getDaySession(todayIso());
      setDayOpen(!!session && (session as { status?: string }).status !== 'closed');
    } catch {
      setDayOpen(false);
    }
  }, []);

  useEffect(() => {
    void (async () => {
      try {
        const rows = (await storePos.listSalesmen()) as SalesmanRow[];
        setSalesmen(Array.isArray(rows) ? rows : []);
      } catch {
        setSalesmen([]);
      }
      await refreshDay();
    })();
  }, [refreshDay]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'F9' || (e.ctrlKey && e.key === 'Enter')) {
        e.preventDefault();
        void openPay();
      }
      if (e.key === 'F8' || (e.ctrlKey && e.key.toLowerCase() === 'h')) {
        e.preventDefault();
        void holdBill();
      }
      if (e.key === 'F2' || (e.ctrlKey && e.key.toLowerCase() === 'n')) {
        e.preventDefault();
        clearBill();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lines, totals, onlineCod, billOnCredit, cash, card, upi, customerPhone, customerName]);

  function clearBill() {
    setLines([]);
    setSkuQuery('');
    setCustomerCode('');
    setCustomerName('');
    setCustomerPhone('');
    setOnlineCod(false);
    setBillOnCredit(false);
    setCash('');
    setCard('');
    setUpi('');
    setStatus('New bill.');
    setError('');
  }

  function addProduct(p: CatalogProduct) {
    const rate = Number(p.storePrice || p.sellingPrice || p.mrp || 0);
    const gst = Number(p.gstPercent || 0);
    setLines((prev) => {
      const existing = prev.find((l) => l.sku === p.sku);
      if (existing) {
        return prev.map((l) =>
          l.sku === p.sku
            ? { ...l, qty: l.qty + 1, amount: lineAmount({ ...l, qty: l.qty + 1 }) }
            : l,
        );
      }
      const draft = {
        lineId: crypto.randomUUID(),
        sku: p.sku,
        name: p.name,
        qty: 1,
        mrp: Number(p.mrp || rate),
        rate,
        discountPercent: 0,
        gstPercent: gst,
        hsnSac: p.hsnSac,
      };
      return [...prev, { ...draft, amount: lineAmount(draft) }];
    });
    setProductOpen(false);
    setSkuQuery('');
    setStatus(`Added ${p.sku}.`);
  }

  async function searchProducts() {
    setError('');
    try {
      const rows = await storePos.searchCatalog(skuQuery.trim() || 'a');
      setProducts(rows);
      setProductOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Catalog search failed');
    }
  }

  async function onSkuEnter() {
    const q = skuQuery.trim();
    if (!q) return;
    setError('');
    try {
      const rows = await storePos.searchCatalog(q);
      if (rows.length === 1) addProduct(rows[0]!);
      else {
        setProducts(rows);
        setProductOpen(true);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Catalog search failed');
    }
  }

  async function searchCustomers() {
    setError('');
    try {
      const rows = (await storePos.listCustomers(customerQ || customerPhone || customerCode)) as CustomerRow[];
      setCustomers(Array.isArray(rows) ? rows : []);
      setCustomerOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Customer search failed');
    }
  }

  async function openHeld() {
    setError('');
    try {
      const rows = (await storePos.listHeldBills()) as Record<string, unknown>[];
      setHeld(Array.isArray(rows) ? rows : []);
      setHeldOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Held bills failed');
    }
  }

  function buildPayload(billNo: string, payments: { method: string; amount: number }[]) {
    const salesman = salesmen.find((s) => (s._id || s.id) === salesmanId);
    return {
      billNo,
      storeId: ctx?.storeId,
      deviceId: ctx?.deviceId,
      posCounter: ctx?.posCounter,
      businessDate: todayIso(),
      customerCode: customerCode || undefined,
      customerName: customerName || 'Walk-in',
      customerPhone: customerPhone || undefined,
      salesmanId: salesmanId || undefined,
      salesmanName: salesman?.name || salesman?.displayLabel,
      holdBills,
      doorDelivery,
      stitching,
      deliveryDate: stitching ? deliveryDate || undefined : undefined,
      printInvoice,
      salesChannel: onlineCod ? 'online' : 'store',
      onlineCod: onlineCod
        ? { status: 'pending', amountDue: totals.payable }
        : undefined,
      billOnCredit,
      lines: lines.map((l, i) => ({
        lineNo: i + 1,
        sku: l.sku,
        name: l.name,
        qty: l.qty,
        mrp: l.mrp,
        rate: l.rate,
        discountPercent: l.discountPercent,
        gstPercent: l.gstPercent,
        hsnSac: l.hsnSac,
        amount: l.amount,
      })),
      totals: {
        subTotal: totals.sub,
        tax: totals.tax,
        payable: totals.payable,
      },
      payments,
      paidAmount: round2(payments.reduce((s, p) => s + p.amount, 0)),
      balanceDue: billOnCredit
        ? round2(totals.payable - payments.reduce((s, p) => s + p.amount, 0))
        : 0,
    };
  }

  async function holdBill() {
    if (!lines.length) {
      setError('Add lines before hold.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      const holdNo = await storePos.nextNumber('holdNo');
      const payload = buildPayload(holdNo, []);
      await storePos.upsertHeldBill(holdNo, { ...payload, holdNo });
      setStatus(`Held ${holdNo}.`);
      clearBill();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : 'Hold failed');
    } finally {
      setBusy(false);
    }
  }

  function openPay() {
    if (!lines.length) {
      setError('Add at least one line.');
      return;
    }
    if (dayOpen === false) {
      setError('Open the day (Day Close) before posting bills.');
      return;
    }
    setCash(onlineCod || billOnCredit ? '0' : String(totals.payable));
    setCard('0');
    setUpi('0');
    setPayOpen(true);
  }

  async function postBill() {
    setBusy(true);
    setError('');
    try {
      const payments: { method: string; amount: number }[] = [];
      const c = Number(cash) || 0;
      const d = Number(card) || 0;
      const u = Number(upi) || 0;
      if (c > 0) payments.push({ method: 'cash', amount: round2(c) });
      if (d > 0) payments.push({ method: 'card', amount: round2(d) });
      if (u > 0) payments.push({ method: 'upi', amount: round2(u) });
      const paid = round2(payments.reduce((s, p) => s + p.amount, 0));
      if (!onlineCod && !billOnCredit && paid + 0.001 < totals.payable) {
        throw new Error('Payment less than payable.');
      }
      const billNo = await storePos.nextNumber('billNo');
      const payload = buildPayload(billNo, onlineCod ? [] : payments);
      await storePos.postBill(payload);
      setPayOpen(false);
      setStatus(`Posted ${billNo}${printInvoice ? ' · print ready' : ''}.`);
      if (printInvoice) {
        window.print();
      }
      clearBill();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : 'Post failed');
    } finally {
      setBusy(false);
    }
  }

  async function openDay() {
    setBusy(true);
    setError('');
    try {
      const openingCash = Number(window.prompt('Opening cash', '0') || '0');
      await storePos.openDaySession({
        businessDate: todayIso(),
        posCounter: ctx?.posCounter,
        status: 'open',
        openingCash,
        openedBy: ctx?.userName || ctx?.email || 'cashier',
      });
      setStatus('Day opened.');
      await refreshDay();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Day open failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      {dayOpen === false ? (
        <div className="card" style={{ marginBottom: 12, background: 'var(--warning-bg)' }}>
          <strong>Day not open.</strong>{' '}
          <button type="button" className="btn btn-primary btn-sm" onClick={() => void openDay()} disabled={busy}>
            Open day
          </button>
        </div>
      ) : null}

      <div className="split-2">
        <div className="card">
          <div className="row-actions" style={{ marginBottom: 8 }}>
            <span className="section-title" style={{ margin: 0 }}>
              Customer
            </span>
            <button type="button" className="btn btn-outline btn-sm" onClick={() => void searchCustomers()}>
              Find Customer
            </button>
            <button type="button" className="btn btn-outline btn-sm" onClick={() => void openHeld()}>
              Resume held
            </button>
          </div>
          <div className="checks">
            <label>
              <input type="checkbox" checked={holdBills} onChange={(e) => setHoldBills(e.target.checked)} /> Hold bills
            </label>
            <label>
              <input type="checkbox" checked={doorDelivery} onChange={(e) => setDoorDelivery(e.target.checked)} /> Door
              delivery
            </label>
            <label>
              <input type="checkbox" checked={onlineCod} onChange={(e) => setOnlineCod(e.target.checked)} /> Online COD
            </label>
            <label>
              <input type="checkbox" checked={stitching} onChange={(e) => setStitching(e.target.checked)} /> Stitching
            </label>
            <label>
              <input type="checkbox" checked={printInvoice} onChange={(e) => setPrintInvoice(e.target.checked)} /> Print
              invoice
            </label>
          </div>
          {stitching ? (
            <div className="row-actions" style={{ marginBottom: 8 }}>
              <span className="form-label">Delivery date</span>
              <input className="field" style={{ maxWidth: 180 }} type="date" value={deliveryDate} onChange={(e) => setDeliveryDate(e.target.value)} />
            </div>
          ) : null}
          <div className="form-grid">
            <span className="form-label">Customer code</span>
            <input className="field" value={customerCode} onChange={(e) => setCustomerCode(e.target.value)} />
            <span className="spacer" />
            <span className="form-label">Salesman</span>
            <select className="field" value={salesmanId} onChange={(e) => setSalesmanId(e.target.value)}>
              <option value="">—</option>
              {salesmen.map((s) => (
                <option key={String(s._id || s.id)} value={String(s._id || s.id)}>
                  {s.displayLabel || s.name || s.code}
                </option>
              ))}
            </select>
            <span className="form-label">Phone</span>
            <input className="field" value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} />
            <span className="spacer" />
            <span className="form-label">Name</span>
            <input className="field" value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
          </div>

          <div className="row-actions" style={{ marginTop: 14 }}>
            <input
              className="field"
              style={{ maxWidth: 280 }}
              placeholder="SKU / barcode / name — Enter"
              value={skuQuery}
              onChange={(e) => setSkuQuery(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') void onSkuEnter();
              }}
            />
            <button type="button" className="btn btn-outline" onClick={() => void searchProducts()}>
              Search products
            </button>
          </div>

          <div style={{ overflow: 'auto', marginTop: 12 }}>
            <table className="data">
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Name</th>
                  <th className="num">Qty</th>
                  <th className="num">Rate</th>
                  <th className="num">Disc%</th>
                  <th className="num">GST%</th>
                  <th className="num">Amount</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {lines.map((l) => (
                  <tr key={l.lineId}>
                    <td>{l.sku}</td>
                    <td>{l.name}</td>
                    <td className="num">
                      <input
                        className="field"
                        style={{ width: 64, padding: 4 }}
                        type="number"
                        min={0.01}
                        step={1}
                        value={l.qty}
                        onChange={(e) => {
                          const qty = Number(e.target.value) || 0;
                          setLines((prev) =>
                            prev.map((x) =>
                              x.lineId === l.lineId ? { ...x, qty, amount: lineAmount({ ...x, qty }) } : x,
                            ),
                          );
                        }}
                      />
                    </td>
                    <td className="num">
                      <input
                        className="field"
                        style={{ width: 80, padding: 4 }}
                        type="number"
                        value={l.rate}
                        onChange={(e) => {
                          const rate = Number(e.target.value) || 0;
                          setLines((prev) =>
                            prev.map((x) =>
                              x.lineId === l.lineId ? { ...x, rate, amount: lineAmount({ ...x, rate }) } : x,
                            ),
                          );
                        }}
                      />
                    </td>
                    <td className="num">
                      <input
                        className="field"
                        style={{ width: 64, padding: 4 }}
                        type="number"
                        value={l.discountPercent}
                        onChange={(e) => {
                          const discountPercent = Number(e.target.value) || 0;
                          setLines((prev) =>
                            prev.map((x) =>
                              x.lineId === l.lineId
                                ? { ...x, discountPercent, amount: lineAmount({ ...x, discountPercent }) }
                                : x,
                            ),
                          );
                        }}
                      />
                    </td>
                    <td className="num">{l.gstPercent}</td>
                    <td className="num">{formatRupee(l.amount)}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-outline btn-sm"
                        onClick={() => setLines((prev) => prev.filter((x) => x.lineId !== l.lineId))}
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                ))}
                {!lines.length ? (
                  <tr>
                    <td colSpan={8} className="msg">
                      Scan or search products to add lines.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </div>

        <div className="card totals-box">
          <div className="section-title">Bill totals</div>
          <div className="line">
            <span>Items</span>
            <span>{lines.reduce((s, l) => s + l.qty, 0)}</span>
          </div>
          <div className="line">
            <span>Subtotal</span>
            <span>{formatRupee(totals.sub)}</span>
          </div>
          <div className="line">
            <span>Tax (incl.)</span>
            <span>{formatRupee(totals.tax)}</span>
          </div>
          <div className="line grand">
            <span>Payable</span>
            <span>{formatRupee(totals.payable)}</span>
          </div>
          <div className="row-actions" style={{ marginTop: 14, flexDirection: 'column', alignItems: 'stretch' }}>
            <button type="button" className="btn btn-outline" onClick={clearBill}>
              New bill (F2)
            </button>
            <button type="button" className="btn btn-outline" disabled={busy} onClick={() => void holdBill()}>
              Hold (F8)
            </button>
            <button type="button" className="btn btn-primary" disabled={busy} onClick={openPay}>
              Payment / Post (F9)
            </button>
          </div>
          <p className="msg" style={{ marginTop: 10 }}>
            {status}
          </p>
          {error ? <p className="msg error">{error}</p> : null}
        </div>
      </div>

      {productOpen ? (
        <Modal
          title="Product search"
          onClose={() => setProductOpen(false)}
          wide
          footer={
            <button type="button" className="btn btn-outline" onClick={() => setProductOpen(false)}>
              Close
            </button>
          }
        >
          <table className="data">
            <thead>
              <tr>
                <th>SKU</th>
                <th>Name</th>
                <th className="num">MRP</th>
                <th className="num">Price</th>
                <th className="num">Stock</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {products.map((p) => (
                <tr key={p.sku}>
                  <td>{p.sku}</td>
                  <td>{p.name}</td>
                  <td className="num">{formatRupee(Number(p.mrp || 0))}</td>
                  <td className="num">{formatRupee(Number(p.storePrice || p.sellingPrice || 0))}</td>
                  <td className="num">{p.stockQty ?? 0}</td>
                  <td>
                    <button type="button" className="btn btn-primary btn-sm" onClick={() => addProduct(p)}>
                      Add
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Modal>
      ) : null}

      {customerOpen ? (
        <Modal title="Find customer" onClose={() => setCustomerOpen(false)} wide>
          <div className="row-actions" style={{ marginBottom: 10 }}>
            <input className="field" value={customerQ} onChange={(e) => setCustomerQ(e.target.value)} placeholder="Name / phone / code" />
            <button type="button" className="btn btn-outline" onClick={() => void searchCustomers()}>
              Search
            </button>
          </div>
          <table className="data">
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Phone</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {customers.map((c) => (
                <tr key={String(c._id || c.id || c.code)}>
                  <td>{c.code}</td>
                  <td>{c.name}</td>
                  <td>{c.phone}</td>
                  <td>
                    <button
                      type="button"
                      className="btn btn-primary btn-sm"
                      onClick={() => {
                        setCustomerCode(c.code || '');
                        setCustomerName(c.name || '');
                        setCustomerPhone(c.phone || '');
                        setCustomerOpen(false);
                      }}
                    >
                      Select
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Modal>
      ) : null}

      {heldOpen ? (
        <Modal title="Held bills" onClose={() => setHeldOpen(false)} wide>
          <table className="data">
            <thead>
              <tr>
                <th>Hold no</th>
                <th>Customer</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {held.map((h) => {
                const holdNo = String(h.holdNo || h._id || '');
                return (
                  <tr key={holdNo}>
                    <td>{holdNo}</td>
                    <td>{String(h.customerName || '')}</td>
                    <td className="row-actions">
                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        onClick={() => {
                          const rawLines = (h.lines as BillLine[]) || [];
                          setLines(
                            rawLines.map((l) => ({
                              ...l,
                              lineId: l.lineId || crypto.randomUUID(),
                              amount: l.amount ?? lineAmount(l),
                            })),
                          );
                          setCustomerCode(String(h.customerCode || ''));
                          setCustomerName(String(h.customerName || ''));
                          setCustomerPhone(String(h.customerPhone || ''));
                          setHeldOpen(false);
                          void storePos.deleteHeldBill(holdNo).catch(() => undefined);
                          setStatus(`Resumed ${holdNo}.`);
                        }}
                      >
                        Resume
                      </button>
                      <button
                        type="button"
                        className="btn btn-outline btn-sm"
                        onClick={() => {
                          void storePos.deleteHeldBill(holdNo).then(() => openHeld());
                        }}
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </Modal>
      ) : null}

      {payOpen ? (
        <Modal
          title="Payment"
          onClose={() => setPayOpen(false)}
          footer={
            <>
              <button type="button" className="btn btn-outline" onClick={() => setPayOpen(false)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void postBill()}>
                Confirm post
              </button>
            </>
          }
        >
          <p>
            Payable <strong>{formatRupee(totals.payable)}</strong>
            {onlineCod ? ' · Online COD (no collection now)' : ''}
          </p>
          <label className="checks">
            <input type="checkbox" checked={billOnCredit} onChange={(e) => setBillOnCredit(e.target.checked)} /> Bill on
            credit
          </label>
          {!onlineCod ? (
            <div className="form-grid" style={{ gridTemplateColumns: '100px 1fr' }}>
              <span className="form-label">Cash</span>
              <input className="field" value={cash} onChange={(e) => setCash(e.target.value)} />
              <span className="form-label">Card</span>
              <input className="field" value={card} onChange={(e) => setCard(e.target.value)} />
              <span className="form-label">UPI</span>
              <input className="field" value={upi} onChange={(e) => setUpi(e.target.value)} />
            </div>
          ) : null}
          {error ? <p className="msg error">{error}</p> : null}
        </Modal>
      ) : null}
    </div>
  );
}
