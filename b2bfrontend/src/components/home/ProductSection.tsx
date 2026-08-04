import { Link } from 'react-router-dom'
import type { Product } from '@/types'
import { ProductCard } from '@/components/product/ProductCard'
import { SectionHeading } from '@/components/ui/SectionHeading'
import { Stagger, StaggerItem } from '@/components/ui/FadeIn'
import { Button } from '@/components/ui/Button'

interface ProductSectionProps {
  eyebrow: string
  title: string
  subtitle: string
  products: Product[]
  viewAllHref?: string
}

export function ProductSection({
  eyebrow,
  title,
  subtitle,
  products,
  viewAllHref,
}: ProductSectionProps) {
  return (
    <section className="bg-canvas-warm/60 py-24">
      <div className="mx-auto max-w-7xl px-4">
        <div className="flex flex-col items-start justify-between gap-6 md:flex-row md:items-end">
          <SectionHeading eyebrow={eyebrow} title={title} subtitle={subtitle} />
          {viewAllHref && (
            <Button to={viewAllHref} variant="ghost" className="shrink-0">
              View all
            </Button>
          )}
        </div>

        <Stagger className="mt-4 grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {products.map((product) => (
            <StaggerItem key={product.id}>
              <ProductCard product={product} />
            </StaggerItem>
          ))}
        </Stagger>

        {viewAllHref && (
          <div className="mt-10 text-center md:hidden">
            <Link to={viewAllHref} className="text-sm font-semibold text-brand hover:text-brand-dark">
              View all products →
            </Link>
          </div>
        )}
      </div>
    </section>
  )
}
