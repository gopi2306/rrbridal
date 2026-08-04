import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import site from '@/data/site'
import { Button } from '@/components/ui/Button'
import { FadeIn } from '@/components/ui/FadeIn'
import { getBridalImage } from '@/lib/images'
import { generalEnquiryUrl } from '@/lib/whatsapp'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

export function BridalSpotlight() {
  const reduced = usePrefersReducedMotion()

  return (
    <section className="relative overflow-hidden border-y border-outline/30 py-24 md:py-32">
      <div className="pointer-events-none absolute inset-0 opacity-40">
        <div className="absolute -left-20 top-0 h-96 w-96 rounded-full bg-brand/30 blur-[120px]" />
        <div className="absolute -right-20 bottom-0 h-96 w-96 rounded-full bg-gold/20 blur-[120px]" />
      </div>

      <div className="relative mx-auto grid max-w-container-max items-center gap-12 px-margin-mobile lg:grid-cols-2 lg:gap-20 md:px-margin-desktop">
        <FadeIn>
          <p className="label-caps text-gold">Signature</p>
          <h2 className="mt-4 font-display text-4xl leading-tight text-ink md:text-5xl lg:text-6xl">
            {site.slogan}
          </h2>
          <p className="mt-6 max-w-lg text-lg leading-relaxed text-ink-muted">
            Premium partywear, heavy handwork suits, and occasion-ready wholesale pieces — crafted
            for retailers who want their racks to feel unforgettable.
          </p>
          <div className="mt-10 flex flex-wrap gap-4">
            <Button to="/premium" variant="primary">
              Premium collection
            </Button>
            <Button to="/partywear" variant="outline">
              Partywear
            </Button>
            <Button href={generalEnquiryUrl()} variant="ghost" external>
              Bridal enquiry
            </Button>
          </div>
        </FadeIn>

        <FadeIn delay={0.1}>
          <motion.div
            className="relative"
            whileHover={reduced ? undefined : { scale: 1.02 }}
            transition={{ duration: 0.5 }}
          >
            <div className="absolute -inset-3 bg-brand/15 blur-2xl" />
            <Link to="/premium" className="relative block overflow-hidden gold-outline">
              <img
                src={getBridalImage()}
                alt="Premium and bridal collection"
                loading="lazy"
                className="aspect-[4/5] w-full object-cover"
              />
              <div className="absolute inset-0 bg-linear-to-t from-canvas/80 via-transparent to-transparent" />
              <div className="absolute bottom-0 left-0 right-0 p-8">
                <p className="label-caps text-brand">Wholesale MOQ from 3 pcs</p>
                <p className="mt-2 font-display text-2xl text-ink">Heavy handwork &amp; occasion wear</p>
              </div>
            </Link>
          </motion.div>
        </FadeIn>
      </div>
    </section>
  )
}
