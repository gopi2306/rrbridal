import { useRef } from 'react'
import { getProductsByFeatured } from '@/lib/data'
import { SectionHeader } from '@/components/ui/SectionHeader'
import { HomeProductCard } from '@/components/home/HomeProductCard'

export function NewArrivalsCarousel() {
  const scrollRef = useRef<HTMLDivElement>(null)
  const products = getProductsByFeatured('new').slice(0, 10)

  const scroll = (dir: -1 | 1) => {
    scrollRef.current?.scrollBy({ left: dir * 280, behavior: 'smooth' })
  }

  return (
    <section className="border-y border-outline bg-canvas-warm">
      <div className="section-pad mx-auto max-w-container-max">
        <div className="flex items-end justify-between gap-4 border-b border-outline pb-4">
          <SectionHeader
            label="Fresh stock"
            title="New arrivals"
            viewAllHref="/readymade"
            bordered={false}
            className="flex-1"
          />
          <div className="hidden shrink-0 gap-2 md:flex">
            <button
              type="button"
              onClick={() => scroll(-1)}
              aria-label="Scroll new arrivals left"
              className="flex h-9 w-9 items-center justify-center border border-outline text-ink-muted hover:text-brand"
            >
              ‹
            </button>
            <button
              type="button"
              onClick={() => scroll(1)}
              aria-label="Scroll new arrivals right"
              className="flex h-9 w-9 items-center justify-center border border-outline text-ink-muted hover:text-brand"
            >
              ›
            </button>
          </div>
        </div>

        <div
          ref={scrollRef}
          className="mt-6 flex gap-4 overflow-x-auto hide-scrollbar snap-x snap-mandatory pb-2"
        >
          {products.map((product) => (
            <div key={product.id} className="w-[min(72vw,240px)] shrink-0 snap-start sm:w-[260px]">
              <HomeProductCard product={product} size="md" />
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
