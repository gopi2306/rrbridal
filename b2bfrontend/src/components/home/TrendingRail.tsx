import { useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import type { Product } from '@/types'
import {
  formatPrice,
  getProductsByCategory,
  getProductsByFeatured,
  products,
} from '@/lib/data'
import { getProductImage } from '@/lib/images'
import { productEnquiryUrl, getProductShareUrl } from '@/lib/whatsapp'
import { productPath } from '@/lib/api'
import { titleCase } from '@/lib/format'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'
import { FadeIn } from '@/components/ui/FadeIn'

const TABS = [
  { id: 'new', label: 'New arrivals' },
  { id: 'month', label: 'Bestsellers' },
  { id: 'summer', label: 'Summer' },
  { id: 'premium', label: 'Premium' },
] as const

type TabId = (typeof TABS)[number]['id']

function getTabProducts(tab: TabId): Product[] {
  if (tab === 'premium') return getProductsByCategory('premium').slice(0, 12)
  return getProductsByFeatured(tab).slice(0, 12)
}

function RailCard({ product }: { product: Product }) {
  const reduced = usePrefersReducedMotion()
  const image = getProductImage(product)
  const whatsappUrl = productEnquiryUrl(product.name, getProductShareUrl(productPath(product)))

  return (
    <motion.article
      className="group w-[72vw] shrink-0 snap-start sm:w-[280px] md:w-[300px]"
      whileHover={reduced ? undefined : { y: -4 }}
      transition={{ duration: 0.3 }}
    >
      <Link to={productPath(product)} className="block">
        <div className="relative aspect-[3/4] overflow-hidden bg-surface gold-outline">
          <img
            src={image}
            alt={product.name}
            loading="lazy"
            className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105"
          />
          <div className="absolute inset-0 bg-linear-to-t from-canvas/90 via-transparent to-transparent opacity-80" />
          <span className="label-caps absolute left-3 top-3 border border-gold/40 bg-canvas/60 px-2 py-1 text-[10px] text-gold backdrop-blur-sm">
            MOQ {product.moq}
          </span>
          <a
            href={whatsappUrl}
            target="_blank"
            rel="noopener noreferrer"
            onClick={(e) => e.stopPropagation()}
            className="label-caps absolute bottom-3 left-3 right-3 border border-[#25D366]/60 bg-[#25D366]/90 py-2.5 text-center text-[10px] text-white opacity-0 transition-opacity group-hover:opacity-100"
          >
            Quick enquiry
          </a>
        </div>
        <div className="mt-4 space-y-1">
          <h3 className="line-clamp-2 font-display text-lg leading-snug text-ink transition-colors group-hover:text-gold">
            {titleCase(product.name)}
          </h3>
          <p className="text-sm font-semibold tabular-nums text-gold">{formatPrice(product.price)}</p>
        </div>
      </Link>
    </motion.article>
  )
}

export function TrendingRail() {
  const [activeTab, setActiveTab] = useState<TabId>('new')
  const scrollRef = useRef<HTMLDivElement>(null)
  const tabProducts = useMemo(() => getTabProducts(activeTab), [activeTab])

  const scroll = (dir: -1 | 1) => {
    scrollRef.current?.scrollBy({ left: dir * 320, behavior: 'smooth' })
  }

  const viewAllHref =
    activeTab === 'premium' ? '/premium' : activeTab === 'summer' ? '/readymade' : '/readymade'

  return (
    <section className="border-y border-outline/30 py-20 md:py-28">
      <div className="mx-auto max-w-container-max px-margin-mobile md:px-margin-desktop">
        <FadeIn className="flex flex-col gap-6 border-b border-outline/30 pb-8 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="label-caps text-gold">Most trending</p>
            <h2 className="mt-3 font-display text-3xl text-ink md:text-5xl">Stock what sells</h2>
          </div>
          <Link to={viewAllHref} className="label-caps text-ink-muted transition-colors hover:text-gold">
            View all →
          </Link>
        </FadeIn>

        <div className="mt-8 flex gap-2 overflow-x-auto hide-scrollbar pb-2">
          {TABS.map((tab) => (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id)}
              className={`label-caps shrink-0 border px-5 py-2.5 text-xs transition-all ${
                activeTab === tab.id
                  ? 'border-brand bg-brand/15 text-brand'
                  : 'border-outline/50 text-ink-muted hover:border-gold/50 hover:text-gold'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="relative mt-8">
          <div
            ref={scrollRef}
            className="flex gap-5 overflow-x-auto hide-scrollbar snap-x snap-mandatory pb-4"
          >
            {tabProducts.map((product) => (
              <RailCard key={product.id} product={product} />
            ))}
            {tabProducts.length === 0 &&
              products.slice(0, 8).map((product) => <RailCard key={product.id} product={product} />)}
          </div>

          <div className="mt-4 hidden justify-end gap-2 md:flex">
            <button
              type="button"
              onClick={() => scroll(-1)}
              aria-label="Scroll products left"
              className="flex h-10 w-10 items-center justify-center border border-outline/50 text-ink-muted transition-colors hover:border-gold hover:text-gold"
            >
              ‹
            </button>
            <button
              type="button"
              onClick={() => scroll(1)}
              aria-label="Scroll products right"
              className="flex h-10 w-10 items-center justify-center border border-outline/50 text-ink-muted transition-colors hover:border-gold hover:text-gold"
            >
              ›
            </button>
          </div>
        </div>
      </div>
    </section>
  )
}
