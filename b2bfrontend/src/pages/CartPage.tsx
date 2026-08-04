import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { BreadcrumbBar } from '@/components/layout/BreadcrumbBar'
import { Button } from '@/components/ui/Button'
import { formatPrice } from '@/lib/data'
import { titleCase } from '@/lib/format'
import { getProductImage } from '@/lib/images'
import { cartEnquiryUrl } from '@/lib/whatsapp'
import { useShop } from '@/context/ShopContext'
import { productPath, submitEnquiry, type EnquiryResponse } from '@/lib/api'

export function CartPage() {
  const { profile, getCartProducts, updateCartQty, removeFromCart, removeCartLines, clearCart } = useShop()
  const lines = getCartProducts()
  const [note, setNote] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState('')
  const [result, setResult] = useState<EnquiryResponse | null>(null)
  const [pendingRequestId, setPendingRequestId] = useState<string | null>(null)
  const subtotal = lines.reduce((sum, { product, quantity }) => sum + product.price * quantity, 0)
  const groups = useMemo(() => {
    const grouped = new Map<string, typeof lines>()
    lines.forEach((line) => {
      const current = grouped.get(line.product.databaseKey) ?? []
      current.push(line)
      grouped.set(line.product.databaseKey, current)
    })
    return [...grouped.values()]
  }, [lines])
  const whatsappUrl = cartEnquiryUrl(
    lines.map(({ product, quantity }) => ({
      name: product.name,
      quantity,
      price: product.price,
      slug: productPath(product),
    })),
  )

  const sendEnquiry = async () => {
    if (!profile || lines.length === 0) return
    setSubmitting(true)
    setSubmitError('')
    setResult(null)
    const requestId = pendingRequestId ?? crypto.randomUUID()
    setPendingRequestId(requestId)
    try {
      const response = await submitEnquiry({
        requestId,
        retailer: profile,
        lines: lines.map(({ product, quantity }) => ({
          databaseKey: product.databaseKey,
          productId: product.productId,
          quantity,
        })),
        note: note.trim() || undefined,
      })
      setResult(response)
      const successfulSources = new Set(
        response.results.map((item) => item.databaseKey),
      )
      const failedSources = new Set(response.errors.map((item) => item.databaseKey).filter(Boolean))
      removeCartLines(
        lines
          .filter(({ product }) => successfulSources.has(product.databaseKey) && !failedSources.has(product.databaseKey))
          .map(({ product }) => product.id),
      )
      if (response.errors.length === 0) setPendingRequestId(null)
    } catch (reason) {
      setSubmitError(reason instanceof Error ? reason.message : 'Unable to submit enquiry.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <BreadcrumbBar items={[{ label: 'Home', href: '/' }, { label: 'Cart' }]} />

      <section className="mx-auto max-w-4xl page-x py-12 md:py-16">
        <h1 className="font-display text-3xl font-semibold text-ink sm:text-4xl">Your cart</h1>
        <p className="mt-2 text-sm text-ink-muted">
          Submit one wholesale enquiry to all selected businesses. No payment is taken online.
        </p>

        {lines.length === 0 ? (
          <div className="mt-12 rounded-2xl border border-dashed border-ink/15 bg-canvas-warm/50 p-12 text-center">
            <p className="text-ink-muted">Your cart is empty.</p>
            <Button to="/readymade" className="mt-6">
              Browse collections
            </Button>
          </div>
        ) : (
          <>
            <div className="mt-10 space-y-8">
              {groups.map((group) => (
                <section key={group[0].product.databaseKey}>
                  <h2 className="mb-3 font-display text-xl text-ink">{group[0].product.databaseLabel}</h2>
                  <ul className="space-y-4">
              {group.map(({ product, quantity }) => (
                <li
                  key={product.id}
                  className="flex flex-col gap-4 rounded-2xl border border-ink/8 bg-surface p-4 sm:flex-row sm:items-center"
                >
                  <img
                    src={getProductImage(product)}
                    alt={product.name}
                    className="h-24 w-20 shrink-0 rounded-xl object-cover"
                  />
                  <div className="min-w-0 flex-1">
                    <Link
                      to={productPath(product)}
                      className="font-display text-lg font-medium text-ink hover:text-brand"
                    >
                      {titleCase(product.name)}
                    </Link>
                    <p className="text-sm text-ink-muted">MOQ {product.moq} pcs · {formatPrice(product.price)} each</p>
                    <p className="mt-1 text-xs text-ink-faint">{product.stock.total} currently available</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <input
                      type="number"
                      min={product.moq}
                      max={product.stock.total}
                      value={quantity}
                      onChange={(e) =>
                        updateCartQty(
                          product.id,
                          Math.min(product.stock.total, Math.max(product.moq, Number(e.target.value) || product.moq)),
                        )
                      }
                      className="w-20 rounded-xl border border-ink/10 px-3 py-2 text-sm"
                      aria-label={`Quantity for ${product.name}`}
                    />
                    <button
                      type="button"
                      onClick={() => removeFromCart(product.id)}
                      className="text-sm text-ink-muted hover:text-brand"
                    >
                      Remove
                    </button>
                  </div>
                  <p className="font-semibold text-ink sm:w-24 sm:text-right">
                    {formatPrice(product.price * quantity)}
                  </p>
                </li>
              ))}
                  </ul>
                </section>
              ))}
            </div>

            <div className="mt-8 rounded-2xl border border-ink/8 bg-canvas-warm/50 p-6">
              <div className="flex items-center justify-between">
                <span className="text-ink-muted">Estimated subtotal</span>
                <span className="text-2xl font-semibold text-ink">{formatPrice(subtotal)}</span>
              </div>
              <p className="mt-2 text-xs text-ink-faint">
                Final pricing and fulfilment are confirmed by each supplying business.
              </p>
              <textarea
                value={note}
                onChange={(event) => setNote(event.target.value)}
                placeholder="Optional note for all businesses"
                className="mt-5 min-h-24 w-full rounded-xl border border-ink/10 bg-surface px-4 py-3 text-sm text-ink"
              />
              {!profile && (
                <div className="mt-5 rounded-xl border border-brand/20 bg-brand/5 p-4 text-sm text-ink-muted">
                  Save your retailer business and contact details before submitting.
                  <Link to="/register" className="ml-2 font-semibold text-brand">Complete retailer profile →</Link>
                </div>
              )}
              {submitError && <p className="mt-4 text-sm text-red-600">{submitError}</p>}
              {result && (
                <div className="mt-5 rounded-xl border border-ink/10 bg-surface p-4 text-sm">
                  <p className="font-semibold text-ink">Enquiry {result.requestId}</p>
                  {result.results.map((item) => (
                    <p key={`${item.databaseKey}-${item.enquiryId}`} className="mt-2 text-emerald-700">
                      {item.databaseLabel}: {item.created ? `created (${item.enquiryId})` : 'not created'}
                    </p>
                  ))}
                  {result.errors.map((item, index) => (
                    <p key={`${item.databaseKey}-${index}`} className="mt-2 text-red-600">
                      {item.databaseLabel || item.databaseKey || 'Business'}: {item.error}
                    </p>
                  ))}
                  {result.errors.length > 0 && <p className="mt-3 text-ink-muted">Failed lines remain in your cart.</p>}
                </div>
              )}
              <div className="mt-6 flex flex-wrap gap-3">
                <Button onClick={sendEnquiry} disabled={!profile || submitting} className="flex-1 sm:flex-none">
                  {submitting ? 'Submitting…' : 'Submit enquiry'}
                </Button>
                <Button href={whatsappUrl} variant="whatsapp" external className="flex-1 sm:flex-none">
                  WhatsApp follow-up
                </Button>
                <Button to="/readymade" variant="ghost">
                  Continue shopping
                </Button>
                <button
                  type="button"
                  onClick={clearCart}
                  className="text-sm text-ink-muted hover:text-brand"
                >
                  Clear cart
                </button>
              </div>
            </div>
          </>
        )}
      </section>
    </>
  )
}
