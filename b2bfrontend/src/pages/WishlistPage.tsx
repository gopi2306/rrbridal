import { BreadcrumbBar } from '@/components/layout/BreadcrumbBar'
import { Button } from '@/components/ui/Button'
import { ProductCard } from '@/components/product/ProductCard'
import { useShop } from '@/context/ShopContext'
import { useCatalog } from '@/context/CatalogContext'

export function WishlistPage() {
  const { wishlist } = useShop()
  const { products: catalogProducts, loading, error, retry } = useCatalog()
  const products = wishlist
    .map((id) => catalogProducts.find((product) => product.id === id))
    .filter((product) => product !== undefined)

  return (
    <>
      <BreadcrumbBar items={[{ label: 'Home', href: '/' }, { label: 'Wishlist' }]} />

      <section className="mx-auto max-w-7xl page-x py-12 md:py-16">
        <h1 className="font-display text-3xl font-semibold text-ink sm:text-4xl">Wishlist</h1>
        <p className="mt-2 text-sm text-ink-muted">
          Saved pieces for your next wholesale order — stored locally in this demo.
        </p>

        {loading ? (
          <p className="mt-12 text-center text-ink-muted">Loading saved products…</p>
        ) : error ? (
          <div className="mt-12 text-center">
            <p className="text-red-600">{error}</p>
            <Button className="mt-4" onClick={retry}>Try again</Button>
          </div>
        ) : products.length === 0 ? (
          <div className="mt-12 rounded-2xl border border-dashed border-ink/15 bg-canvas-warm/50 p-12 text-center">
            <p className="text-ink-muted">No saved items yet.</p>
            <Button to="/readymade" className="mt-6">
              Explore collections
            </Button>
          </div>
        ) : (
          <div className="mt-10 grid gap-8 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {products.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
        )}
      </section>
    </>
  )
}
