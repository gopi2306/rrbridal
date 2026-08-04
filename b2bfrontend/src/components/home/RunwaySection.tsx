import { Link } from 'react-router-dom'
import { FadeIn } from '@/components/ui/FadeIn'
import { getBridalImage } from '@/lib/images'

export function RunwaySection() {
  return (
    <section className="relative my-20 w-full md:my-32">
      <FadeIn className="relative h-[55vh] min-h-[360px] md:h-[75vh]">
        <img
          src={getBridalImage()}
          alt="Premium and bridal wholesale collection"
          loading="lazy"
          className="h-full w-full object-cover"
        />
        <div className="absolute inset-0 bg-linear-to-t from-canvas via-canvas/20 to-transparent" />
        <div className="absolute bottom-0 left-0 right-0 z-10 flex flex-col items-start justify-between gap-6 p-margin-mobile md:flex-row md:items-end md:p-margin-desktop">
          <div>
            <p className="label-caps text-brand">Immersive collection</p>
            <h2 className="mt-2 font-display text-4xl text-ink md:text-6xl">The Bridal Edit</h2>
            <p className="mt-3 max-w-md text-sm text-ink-muted md:text-base">
              Heavy handwork, premium partywear, and occasion sets — wholesale MOQ from 3 pcs.
            </p>
          </div>
          <Link
            to="/premium"
            className="label-caps shrink-0 border border-gold/50 bg-canvas/30 px-8 py-4 text-gold backdrop-blur-md transition-all hover:bg-gold hover:text-obsidian"
          >
            Shop premium
          </Link>
        </div>
      </FadeIn>
    </section>
  )
}
