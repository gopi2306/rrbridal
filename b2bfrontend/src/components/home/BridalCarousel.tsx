import { useRef } from 'react'
import { Link } from 'react-router-dom'
import { getProductsByCategory } from '@/lib/data'
import { getBridalBanner } from '@/lib/images'
import { SafeImage } from '@/components/ui/SafeImage'
import { SectionHeader } from '@/components/ui/SectionHeader'
import { HomeProductCard } from '@/components/home/HomeProductCard'

export function BridalCarousel() {
  const scrollRef = useRef<HTMLDivElement>(null)
  const products = getProductsByCategory('premium').slice(0, 8)
  const banner = getBridalBanner()

  const scroll = (dir: -1 | 1) => {
    scrollRef.current?.scrollBy({ left: dir * 340, behavior: 'smooth' })
  }

  return (
    <section className="section-pad mx-auto max-w-container-max">
      <div className="grid gap-6 lg:grid-cols-[1fr_1.2fr] lg:items-stretch">
        <Link to="/premium" className="group relative min-h-[320px] overflow-hidden lg:min-h-full">
          <SafeImage
            src={banner.src}
            fallback={banner.fallback}
            alt="Premium and bridal wholesale"
            className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105"
          />
          <div className="absolute inset-0 bg-linear-to-t from-canvas via-canvas/20 to-transparent" />
          <div className="absolute bottom-0 left-0 p-6">
            <p className="label-caps text-ink-muted">Occasion wear</p>
            <h2 className="mt-2 text-2xl font-semibold text-ink md:text-3xl">Bridal &amp; premium</h2>
            <span className="label-caps mt-4 inline-block text-brand">Explore →</span>
          </div>
        </Link>

        <div>
          <SectionHeader label="Heavy handwork" title="Premium line" viewAllHref="/premium" />
          <div className="relative mt-6">
            <div ref={scrollRef} className="flex gap-4 overflow-x-auto hide-scrollbar pb-2">
              {products.map((product, i) => (
                <div
                  key={product.id}
                  className="shrink-0"
                  style={{ width: i % 2 === 0 ? '260px' : '220px' }}
                >
                  <HomeProductCard product={product} size="sm" showEnquiry={false} />
                </div>
              ))}
            </div>
            <div className="mt-4 hidden justify-end gap-2 lg:flex">
              <button
                type="button"
                onClick={() => scroll(-1)}
                aria-label="Scroll left"
                className="flex h-9 w-9 items-center justify-center border border-outline text-ink-muted hover:text-brand"
              >
                ‹
              </button>
              <button
                type="button"
                onClick={() => scroll(1)}
                aria-label="Scroll right"
                className="flex h-9 w-9 items-center justify-center border border-outline text-ink-muted hover:text-brand"
              >
                ›
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
