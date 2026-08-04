import { Link } from 'react-router-dom'
import { FadeIn } from '@/components/ui/FadeIn'
import { getHeroImage } from '@/lib/images'

export function CraftFeature() {
  return (
    <section className="mx-auto max-w-container-max px-margin-mobile py-16 md:px-margin-desktop md:py-24">
      <div className="grid grid-cols-1 items-center gap-gutter md:grid-cols-12">
        <FadeIn className="group relative md:col-span-7">
          <div className="absolute inset-0 -z-10 translate-x-3 translate-y-3 bg-gold/10 transition-transform duration-500 group-hover:translate-x-5 group-hover:translate-y-5" />
          <img
            src={getHeroImage()}
            alt="Premium textile craftsmanship"
            loading="lazy"
            className="aspect-[1/1.15] w-full object-cover grayscale-[15%] transition-all duration-700 group-hover:grayscale-0 gold-outline"
          />
          <div className="glass-nav absolute bottom-6 right-4 max-w-xs p-6 shadow-elevated md:-right-10 md:p-8">
            <p className="label-caps text-gold">The loom of light</p>
            <h3 className="mt-2 font-display text-2xl text-ink">Embroidery that holds form</h3>
            <p className="mt-2 text-sm leading-relaxed text-ink-muted">
              Handwork, jacquard, and festive finishes engineered for bulk repeat orders.
            </p>
          </div>
        </FadeIn>

        <FadeIn delay={0.1} className="mt-12 md:col-span-4 md:col-start-9 md:mt-0">
          <p className="label-caps text-ink-faint">Wholesale focus</p>
          <h3 className="mt-4 font-display text-3xl text-ink md:text-4xl">Structural quality.</h3>
          <p className="mt-6 text-base leading-relaxed text-ink-muted">
            Every piece is selected for drape, stitch density, and sell-through — so your racks move
            faster. MOQ-friendly packs from Surat &amp; Hyderabad, shipped to retailers nationwide.
          </p>
          <Link
            to="/dress-material"
            className="label-caps mt-8 inline-flex items-center gap-2 border-b border-gold/40 pb-1 text-gold transition-colors hover:text-brand"
          >
            View dress materials
            <span aria-hidden>→</span>
          </Link>
        </FadeIn>
      </div>
    </section>
  )
}
