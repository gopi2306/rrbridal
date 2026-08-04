import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { getFeaturedCategories } from '@/lib/data'
import { FadeIn } from '@/components/ui/FadeIn'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

export function CategoryPortals() {
  const reduced = usePrefersReducedMotion()
  const featured = getFeaturedCategories(4)

  return (
    <section className="mx-auto max-w-container-max px-margin-mobile py-20 md:px-margin-desktop md:py-28">
      <FadeIn className="mb-12 max-w-2xl md:mb-16">
        <p className="label-caps text-gold">Collections</p>
        <h2 className="mt-3 font-display text-3xl text-ink md:text-5xl">Enter the catalog</h2>
        <p className="mt-4 text-lg text-ink-muted">
          Four wholesale entry points — salwar, partywear, Pakistani suits, and premium bridal stock.
        </p>
      </FadeIn>

      <div className="grid gap-4 md:grid-cols-12 md:gap-gutter">
        {featured.map((cat, index) => {
          const isLarge = index === 0 || index === 3
          return (
            <FadeIn
              key={cat.id}
              delay={index * 0.06}
              className={isLarge ? 'md:col-span-7' : 'md:col-span-5'}
            >
              <Link to={cat.href} className="group block">
                <motion.div
                  className={`relative overflow-hidden gold-outline ${index === 1 ? 'md:mt-16' : ''} ${index === 2 ? 'md:-mt-8' : ''}`}
                  whileHover={reduced ? undefined : { y: -3 }}
                  transition={{ duration: 0.35 }}
                >
                  <div className={isLarge ? 'aspect-[16/10]' : 'aspect-[4/5] md:aspect-[16/11]'}>
                    <img
                      src={cat.image}
                      alt={cat.title}
                      loading="lazy"
                      className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105"
                    />
                  </div>
                  <div className="absolute inset-0 bg-linear-to-t from-canvas via-canvas/30 to-transparent" />
                  <div className="absolute inset-0 flex flex-col justify-end p-6 md:p-8">
                    <p className="label-caps text-brand-soft">0{index + 1}</p>
                    <h3 className="mt-2 font-display text-2xl text-ink md:text-4xl">{cat.title}</h3>
                    <p className="mt-2 max-w-md text-sm text-ink-muted">{cat.description}</p>
                    <span className="label-caps mt-5 inline-flex items-center gap-2 text-gold">
                      Shop wholesale
                      <span className="transition-transform group-hover:translate-x-1">→</span>
                    </span>
                  </div>
                </motion.div>
              </Link>
            </FadeIn>
          )
        })}
      </div>

      <p className="mt-10 text-center">
        <Link to="/salwar-kameez" className="label-caps text-gold hover:text-brand">
          View all 11 collections →
        </Link>
      </p>
    </section>
  )
}
