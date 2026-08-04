import { useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import type { Product } from '@/types'
import { getProductGallery } from '@/lib/images'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

interface ProductGalleryProps {
  product: Product
}

export function ProductGallery({ product }: ProductGalleryProps) {
  const images = getProductGallery(product)
  const [active, setActive] = useState(0)
  const reduced = usePrefersReducedMotion()

  const prev = () => setActive((i) => (i - 1 + images.length) % images.length)
  const next = () => setActive((i) => (i + 1) % images.length)

  return (
    <div className="space-y-4">
      <div className="group relative">
        <div className="flex aspect-[3/4] w-full items-center justify-center overflow-hidden rounded-[1.5rem] bg-canvas-warm ring-1 ring-ink/8">
          <AnimatePresence mode="wait">
            <motion.img
              key={images[active]}
              src={images[active]}
              alt={product.name}
              className="h-full w-full object-contain p-3 md:p-4"
              initial={reduced ? false : { opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={reduced ? undefined : { opacity: 0 }}
              transition={{ duration: 0.25 }}
            />
          </AnimatePresence>
        </div>

        {images.length > 1 && (
          <>
            <button
              type="button"
              onClick={prev}
              aria-label="Previous image"
              title="Previous image"
              className="absolute left-2 top-1/2 flex h-10 w-10 -translate-y-1/2 cursor-pointer items-center justify-center rounded-full bg-surface/90 text-lg text-ink shadow-md opacity-90 transition-opacity hover:bg-surface lg:opacity-0 lg:group-hover:opacity-100"
            >
              ‹
            </button>
            <button
              type="button"
              onClick={next}
              aria-label="Next image"
              title="Next image"
              className="absolute right-2 top-1/2 flex h-10 w-10 -translate-y-1/2 cursor-pointer items-center justify-center rounded-full bg-surface/90 text-lg text-ink shadow-md opacity-90 transition-opacity hover:bg-surface lg:opacity-0 lg:group-hover:opacity-100"
            >
              ›
            </button>
          </>
        )}
      </div>

      {images.length > 1 && (
        <div className="grid grid-cols-3 gap-3">
          {images.map((src, i) => (
            <button
              key={`${src}-${i}`}
              type="button"
              onClick={() => setActive(i)}
              aria-label={`View image ${i + 1}`}
              className={`flex aspect-[3/4] cursor-pointer items-center justify-center overflow-hidden rounded-xl bg-canvas-warm ring-2 transition-all ${
                active === i ? 'ring-brand' : 'ring-transparent opacity-70 hover:opacity-100'
              }`}
            >
              <img src={src} alt="" className="h-full w-full object-contain p-1" />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
