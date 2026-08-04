import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { BreadcrumbBar } from '@/components/layout/BreadcrumbBar'
import { Button } from '@/components/ui/Button'
import { ProductGallery } from '@/components/product/ProductGallery'
import { RelatedProducts } from '@/components/product/RelatedProducts'
import { formatPrice, getCategoryById } from '@/lib/data'
import { titleCase } from '@/lib/format'
import { getProductShareUrl, productEnquiryUrl } from '@/lib/whatsapp'
import { useShop } from '@/context/ShopContext'
import { Tooltip } from '@/components/ui/Tooltip'
import { fetchProduct, productPath } from '@/lib/api'
import { useCatalog } from '@/context/CatalogContext'
import type { Product } from '@/types'

export function ProductPage() {
  const { databaseKey, productId } = useParams()
  const { products, loading: catalogLoading } = useCatalog()
  const cached = products.find((item) => item.databaseKey === databaseKey && item.productId === productId)
  const [product, setProduct] = useState<Product | undefined>(cached)
  const [loading, setLoading] = useState(!cached)
  const [loadError, setLoadError] = useState('')
  const { addToCart, toggleWishlist, isInWishlist } = useShop()
  const [qty, setQty] = useState(product?.moq ?? 1)
  const [added, setAdded] = useState(false)

  useEffect(() => {
    if (cached) {
      setProduct(cached)
      setQty(cached.moq)
      setLoading(false)
      return
    }
    if (!databaseKey || !productId || catalogLoading) return
    const controller = new AbortController()
    setLoading(true)
    fetchProduct(databaseKey, productId, controller.signal)
      .then((offer) => {
        setProduct({ ...offer, views: 0, categoryPath: offer.category.key })
        setQty(offer.moq)
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setLoadError(reason instanceof Error ? reason.message : 'Product unavailable')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [cached, databaseKey, productId, catalogLoading])

  if (loading || catalogLoading) {
    return <div className="mx-auto max-w-7xl page-x py-24 text-center text-ink-muted">Loading product…</div>
  }

  if (!product) {
    return (
      <>
        <BreadcrumbBar items={[{ label: 'Home', href: '/' }, { label: 'Product not found' }]} />
        <div className="mx-auto max-w-7xl page-x py-20 text-center">
          {loadError && <p className="mb-4 text-sm text-red-600">{loadError}</p>}
          <Link to="/" className="text-brand hover:text-brand-dark">← Back to home</Link>
        </div>
      </>
    )
  }

  const whatsappUrl = productEnquiryUrl(product.name, getProductShareUrl(productPath(product)))
  const categoryId = product.category.key
  const category = getCategoryById(categoryId)
  const wished = isInWishlist(product.id)
  const available = product.stock.available && product.stock.total >= product.moq

  const handleAddToCart = () => {
    addToCart(product, Math.max(qty, product.moq))
    setAdded(true)
    window.setTimeout(() => setAdded(false), 2000)
  }

  return (
    <>
      <BreadcrumbBar
        items={[
          { label: 'Home', href: '/' },
          { label: category?.title ?? 'Collections', href: `/${categoryId}` },
          { label: titleCase(product.name) },
        ]}
      />

      <section className="mx-auto max-w-7xl page-x py-8 pb-28 md:py-12 md:pb-12">
        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(0,420px)] lg:gap-12 lg:items-start">
          <ProductGallery product={product} />

          <div className="lg:sticky lg:top-[calc(var(--header-height)+1rem)]">
            <p className="text-xs font-semibold uppercase tracking-[0.25em] text-brand">{product.databaseLabel}</p>
            <h1 className="mt-3 font-display text-2xl font-semibold leading-tight text-ink sm:text-3xl md:text-4xl">
              {titleCase(product.name)}
            </h1>
            <p className="mt-4 text-2xl font-semibold text-ink">{formatPrice(product.price)}</p>
            <p className="mt-1 text-sm text-ink-muted">
              Minimum order: {product.moq} pieces · Prices in INR
            </p>
            <p className={`mt-2 text-sm font-medium ${available ? 'text-emerald-700' : 'text-red-600'}`}>
              {available ? `${product.stock.total} units available` : 'Currently unavailable'}
            </p>

            <div className="mt-6 flex flex-wrap items-center gap-3">
              <label className="text-sm text-ink-muted" htmlFor="product-qty">
                Quantity
              </label>
              <input
                id="product-qty"
                type="number"
                min={product.moq}
                max={product.stock.total}
                value={qty}
                onChange={(e) =>
                  setQty(Math.min(product.stock.total, Math.max(product.moq, Number(e.target.value) || product.moq)))
                }
                className="w-24 rounded-xl border border-ink/10 bg-surface px-3 py-2 text-sm text-ink"
              />
            </div>

            <div className="mt-6 flex flex-wrap gap-3">
              <Button onClick={handleAddToCart} disabled={!available} className="flex-1 sm:flex-none">
                {added ? 'Added to cart' : 'Add to cart'}
              </Button>
              <Tooltip label={wished ? 'Remove from wishlist' : 'Add to wishlist'} side="top">
                <button
                  type="button"
                  onClick={() => toggleWishlist(product.id)}
                  aria-label={wished ? 'Remove from wishlist' : 'Add to wishlist'}
                  className={`inline-flex h-11 w-11 cursor-pointer items-center justify-center rounded-full border transition-colors ${
                    wished ? 'border-brand bg-brand text-white' : 'border-ink/10 text-ink-muted hover:border-brand hover:text-brand'
                  }`}
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill={wished ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="2" aria-hidden>
                    <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                  </svg>
                </button>
              </Tooltip>
            </div>

            <div className="mt-6 flex flex-wrap gap-3">
              <Button href={whatsappUrl} variant="whatsapp" external className="flex-1 sm:flex-none">
                Enquire on WhatsApp
              </Button>
              <Button to={`/${categoryId}`} variant="ghost" className="flex-1 sm:flex-none">
                More in collection
              </Button>
            </div>

            <div className="mt-8 rounded-2xl border border-ink/8 bg-surface p-5">
              <h2 className="text-sm font-semibold uppercase tracking-widest text-ink-faint">Specifications</h2>
              <dl className="mt-4 grid grid-cols-2 gap-4 text-sm">
                {Object.entries(product.specifications).map(([label, value]) => (
                  <div key={label}>
                    <dt className="text-ink-faint">{titleCase(label)}</dt>
                    <dd className="font-medium text-ink">{value}</dd>
                  </div>
                ))}
              </dl>
            </div>

            <p className="mt-5 text-sm leading-relaxed text-ink-muted">
              Add this style to your enquiry cart. WhatsApp remains available for colour and bulk-pricing follow-up.
            </p>
          </div>
        </div>
      </section>

      <div className="safe-bottom fixed inset-x-0 bottom-0 z-30 flex gap-2 border-t border-ink/8 bg-surface/95 p-3 backdrop-blur-lg lg:hidden">
        <Button href={whatsappUrl} variant="whatsapp" external className="flex-1">
          WhatsApp
        </Button>
        <Button onClick={handleAddToCart} disabled={!available} className="flex-1">
          Add to cart
        </Button>
      </div>

      <RelatedProducts product={product} />
    </>
  )
}
