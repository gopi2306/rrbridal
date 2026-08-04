import type { Product } from '@/types'
import { getRelatedProducts } from '@/lib/data'
import { ProductCard } from './ProductCard'
import { SectionHeading } from '@/components/ui/SectionHeading'
import { Stagger, StaggerItem } from '@/components/ui/FadeIn'
import { useCatalog } from '@/context/CatalogContext'

interface RelatedProductsProps {
  product: Product
}

export function RelatedProducts({ product }: RelatedProductsProps) {
  const { products } = useCatalog()
  const related = getRelatedProducts(product, 4, products)
  if (related.length === 0) return null

  return (
    <section className="border-t border-ink/5 bg-canvas-warm/40 py-14 md:py-20">
      <div className="mx-auto max-w-7xl page-x">
        <SectionHeading eyebrow="You may also like" title="More from this collection" />
        <Stagger className="grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {related.map((item) => (
            <StaggerItem key={item.id}>
              <ProductCard product={item} />
            </StaggerItem>
          ))}
        </Stagger>
      </div>
    </section>
  )
}
