import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { categories } from '@/lib/data'
import { SectionHeading } from '@/components/ui/SectionHeading'
import { Stagger, StaggerItem } from '@/components/ui/FadeIn'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

export function CategoryShowcase() {
  const reduced = usePrefersReducedMotion()

  return (
    <section className="mx-auto max-w-7xl px-4 py-24">
      <SectionHeading
        eyebrow="Collections"
        title="Shop by Category"
        subtitle="Explore our wholesale ethnic wear collections — curated for retailers and boutique owners."
        align="center"
      />

      <Stagger className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {categories.map((cat, index) => (
          <StaggerItem key={cat.id}>
            <Link to={cat.href} className="group block">
              <motion.div
                className="relative aspect-[4/5] overflow-hidden rounded-3xl"
                whileHover={reduced ? undefined : { scale: 1.02 }}
                transition={{ duration: 0.4 }}
              >
                <img
                  src={cat.image}
                  alt={cat.title}
                  loading="lazy"
                  className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-110"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-ink/80 via-ink/20 to-transparent" />
                <div className="absolute inset-x-0 bottom-0 p-6 text-white">
                  <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand-soft">
                    0{index + 1}
                  </p>
                  <h3 className="mt-2 font-display text-2xl font-semibold">{cat.title}</h3>
                  <p className="mt-2 line-clamp-2 text-sm text-white/75 opacity-0 transition-opacity duration-300 group-hover:opacity-100">
                    {cat.description}
                  </p>
                  <span className="mt-4 inline-flex items-center gap-2 text-sm font-medium text-brand-soft">
                    Explore
                    <span className="transition-transform group-hover:translate-x-1">→</span>
                  </span>
                </div>
              </motion.div>
            </Link>
          </StaggerItem>
        ))}
      </Stagger>
    </section>
  )
}
