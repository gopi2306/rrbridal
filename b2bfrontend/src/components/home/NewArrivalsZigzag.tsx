import { getProductsByFeatured } from '@/lib/data'
import { SectionHeader } from '@/components/ui/SectionHeader'
import { HomeProductCard } from '@/components/home/HomeProductCard'

export function NewArrivalsZigzag() {
  const products = getProductsByFeatured('new').slice(0, 6)

  return (
    <section className="border-y border-outline bg-canvas-warm">
      <div className="section-pad mx-auto max-w-container-max">
        <SectionHeader label="Fresh stock" title="New arrivals" viewAllHref="/readymade" />

        <div className="mt-8 grid grid-cols-2 gap-3 md:grid-cols-3 md:gap-4">
          {products.map((product, i) => (
            <div
              key={product.id}
              className={
                i % 3 === 0
                  ? 'col-span-2 row-span-2 md:col-span-1 md:row-span-2'
                  : i % 3 === 1
                    ? 'md:mt-12'
                    : ''
              }
            >
              <HomeProductCard product={product} size={i % 3 === 0 ? 'lg' : 'sm'} />
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
