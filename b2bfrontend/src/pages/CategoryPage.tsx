import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { AnimatePresence, motion } from 'framer-motion'
import { PageHero } from '@/components/layout/PageHero'
import { ProductCard } from '@/components/product/ProductCard'
import { ActiveFilterChips, CatalogToolbar } from '@/components/catalog/CatalogToolbar'
import { ProductFilters } from '@/components/catalog/ProductFilters'
import { Button } from '@/components/ui/Button'
import { categories, getCategoryById } from '@/lib/data'
import { useCatalog } from '@/context/CatalogContext'
import {
  applyProductFilters,
  buildFilterMeta,
  countActiveFilters,
  createDefaultFilters,
  type ProductFiltersState,
  type SortOption,
} from '@/lib/filters'
import type { Breadcrumb } from '@/types'

export function CategoryPage() {
  const { categoryId, '*': subPath } = useParams()
  const { products: catalogProducts, categories: liveCategories, loading, error, retry } = useCatalog()
  const staticCategory = categoryId ? getCategoryById(categoryId) : undefined
  const liveCategory = liveCategories.find((item) => item.key === categoryId)
  const productCategory = catalogProducts.find((item) => item.category.key === categoryId)?.category
  const discoveredCategory = liveCategory ?? (productCategory ? {
    key: productCategory.key,
    name: productCategory.name,
    productCount: catalogProducts.filter((item) => item.category.key === productCategory.key).length,
  } : undefined)
  const category = staticCategory ?? (discoveredCategory ? {
    id: discoveredCategory.key,
    title: discoveredCategory.name,
    href: `/${discoveredCategory.key}`,
    image: '/images/banners/banner-01.webp',
    description: `${discoveredCategory.productCount} live wholesale styles`,
    children: [],
  } : undefined)
  const sub = subPath?.replace(/^\//, '') || undefined
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false)

  const products = useMemo(
    () => catalogProducts.filter((product) => {
      if (product.category.key !== categoryId) return false
      if (!sub) return true
      const subKey = typeof product.subCategory === 'string'
        ? product.subCategory
        : product.subCategory?.key
      return subKey === sub
    }),
    [catalogProducts, categoryId, sub],
  )

  const meta = useMemo(
    () => buildFilterMeta(products, sub ? undefined : category?.children),
    [products, sub, category?.children],
  )

  const defaults = useMemo(() => createDefaultFilters(meta), [meta])
  const [filters, setFilters] = useState<ProductFiltersState>(() => createDefaultFilters(meta))

  useEffect(() => {
    setFilters(createDefaultFilters(meta))
  }, [category?.id, sub, meta])

  const filteredProducts = useMemo(() => applyProductFilters(products, filters), [products, filters])
  const activeCount = countActiveFilters(filters, defaults)

  const resetFilters = () => setFilters(defaults)

  const handleSortChange = (sort: SortOption) => setFilters((prev) => ({ ...prev, sort }))

  if (loading) {
    return <div className="mx-auto max-w-7xl page-x py-24 text-center text-ink-muted">Loading live catalogue…</div>
  }

  if (error) {
    return (
      <div className="mx-auto max-w-3xl page-x py-24 text-center">
        <p className="font-display text-2xl text-ink">Catalogue unavailable</p>
        <p className="mt-3 text-sm text-ink-muted">{error}</p>
        <Button className="mt-6" onClick={retry}>Try again</Button>
      </div>
    )
  }

  if (!category) {
    return (
      <>
        <PageHero title="Collections" subtitle="Browse all wholesale categories." />
        <section className="mx-auto max-w-7xl page-x py-16">
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {categories.map((cat) => (
              <Link
                key={cat.id}
                to={cat.href}
                className="group overflow-hidden rounded-3xl border border-ink/8 bg-surface shadow-soft"
              >
                <img src={cat.image} alt={cat.title} className="aspect-[16/10] w-full object-cover transition-transform duration-500 group-hover:scale-105" />
                <div className="p-6">
                  <h2 className="font-display text-2xl font-semibold text-ink">{cat.title}</h2>
                  <p className="mt-2 text-sm text-ink-muted">{cat.description}</p>
                </div>
              </Link>
            ))}
          </div>
        </section>
      </>
    )
  }

  const subLabel = sub
    ? category.children.find((c) => c.href.endsWith(sub))?.label
    : undefined

  return (
    <>
      <PageHero
        title={subLabel ?? category.title}
        subtitle={category.description}
        breadcrumb={
          [
            { label: 'Home', href: '/' },
            { label: category.title, href: category.href },
            ...(subLabel ? [{ label: subLabel }] : []),
          ] satisfies Breadcrumb[]
        }
      />

      {(liveCategories.length > 0 || category.children.length > 0) && !sub && (
        <section className="border-b border-ink/5 bg-surface">
          <div className="mx-auto max-w-7xl page-x py-6">
            {liveCategories.length > 0 && (
              <p className="mb-3 text-xs font-semibold uppercase tracking-widest text-ink-faint">
                Live database categories
              </p>
            )}
            <div className="flex flex-wrap gap-2">
            {liveCategories.map((item) => (
              <Link
                key={`live-${item.key}`}
                to={`/${item.key}`}
                className={`rounded-full border px-4 py-2 text-sm font-medium transition-colors ${
                  item.key === categoryId
                    ? 'border-brand bg-brand text-white'
                    : 'border-ink/10 text-ink-muted hover:border-brand hover:text-brand'
                }`}
              >
                {item.name} ({item.productCount})
              </Link>
            ))}
            {category.children.map((child) => (
              <Link
                key={child.href}
                to={child.href}
                className="rounded-full border border-ink/10 px-4 py-2 text-sm font-medium text-ink-muted transition-colors hover:border-brand hover:text-brand"
              >
                {child.label}
              </Link>
            ))}
            </div>
          </div>
        </section>
      )}

      <section className="mx-auto max-w-7xl page-x py-12 md:py-16">
        {products.length > 0 ? (
          <div className="lg:grid lg:grid-cols-[260px_minmax(0,1fr)] lg:gap-10 xl:grid-cols-[280px_minmax(0,1fr)]">
            <aside className="hidden lg:block lg:self-start">
              <div className="sticky top-[calc(var(--header-height)+1rem)] flex max-h-[calc(100dvh-var(--header-height)-2rem)] flex-col overflow-hidden rounded-2xl border border-ink/8 bg-surface shadow-soft">
                <div className="flex shrink-0 items-center justify-between gap-3 border-b border-ink/8 px-5 py-4">
                  <div>
                    <h2 className="font-display text-lg font-semibold text-ink">Filters</h2>
                    <p className="mt-0.5 text-xs text-ink-muted">
                      {activeCount > 0
                        ? `${activeCount} active`
                        : 'Refine your catalogue'}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={resetFilters}
                    className="shrink-0 cursor-pointer text-xs font-semibold text-brand hover:text-brand-dark"
                  >
                    Clear all
                  </button>
                </div>
                <div className="min-h-0 flex-1 overflow-y-auto overscroll-y-contain px-5 py-3">
                  <ProductFilters
                    meta={meta}
                    state={filters}
                    defaults={defaults}
                    onChange={setFilters}
                    onReset={resetFilters}
                    showHeader={false}
                  />
                </div>
              </div>
            </aside>

            <div>
              <CatalogToolbar
                total={products.length}
                filtered={filteredProducts.length}
                activeFilters={activeCount}
                sort={filters.sort}
                onSortChange={handleSortChange}
                onOpenFilters={() => setMobileFiltersOpen(true)}
              />

              <ActiveFilterChips
                state={filters}
                defaults={defaults}
                meta={meta}
                onChange={setFilters}
                onReset={resetFilters}
              />

              {filteredProducts.length > 0 ? (
                <div className="grid gap-8 sm:grid-cols-2 xl:grid-cols-3">
                  {filteredProducts.map((product) => (
                    <ProductCard key={product.id} product={product} />
                  ))}
                </div>
              ) : (
                <div className="rounded-3xl border border-dashed border-ink/15 bg-canvas-warm/50 px-8 py-16 text-center">
                  <p className="font-display text-2xl text-ink">No products match your filters</p>
                  <p className="mt-3 text-ink-muted">Try adjusting price range or clearing filters.</p>
                  <button
                    type="button"
                    onClick={resetFilters}
                    className="mt-6 cursor-pointer text-sm font-semibold text-brand hover:text-brand-dark"
                  >
                    Clear all filters
                  </button>
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="rounded-3xl border border-dashed border-ink/15 bg-canvas-warm/50 px-8 py-20 text-center">
            <p className="font-display text-2xl text-ink">No live products in this collection</p>
            <p className="mt-3 text-ink-muted">
              New products for this collection are being added. Enquire on WhatsApp for the latest stock.
            </p>
            <Link to="/contact" className="mt-6 inline-block text-sm font-semibold text-brand hover:text-brand-dark">
              Contact us →
            </Link>
          </div>
        )}
      </section>

      <AnimatePresence>
        {mobileFiltersOpen && (
          <>
            <motion.button
              type="button"
              aria-label="Close filters"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="fixed inset-0 z-50 cursor-pointer bg-ink/50 lg:hidden"
              onClick={() => setMobileFiltersOpen(false)}
            />
            <motion.aside
              initial={{ x: '100%' }}
              animate={{ x: 0 }}
              exit={{ x: '100%' }}
              transition={{ type: 'spring', damping: 28, stiffness: 320 }}
              className="fixed inset-y-0 right-0 z-50 flex w-full max-w-sm flex-col bg-surface shadow-elevated lg:hidden"
            >
              <div className="flex items-center justify-between border-b border-ink/8 px-5 py-4">
                <h2 className="font-display text-xl font-semibold text-ink">Filters</h2>
                <button
                  type="button"
                  onClick={() => setMobileFiltersOpen(false)}
                  aria-label="Close filters"
                  className="flex h-9 w-9 cursor-pointer items-center justify-center rounded-full border border-ink/10 text-ink-muted hover:text-brand"
                >
                  ×
                </button>
              </div>
              <div className="flex-1 overflow-y-auto px-5 py-4">
                <ProductFilters
                  idPrefix="mobile"
                  meta={meta}
                  state={filters}
                  defaults={defaults}
                  onChange={setFilters}
                  onReset={resetFilters}
                />
              </div>
              <div className="border-t border-ink/8 p-5">
                <Button className="w-full" onClick={() => setMobileFiltersOpen(false)}>
                  Show {filteredProducts.length} results
                </Button>
              </div>
            </motion.aside>
          </>
        )}
      </AnimatePresence>
    </>
  )
}
