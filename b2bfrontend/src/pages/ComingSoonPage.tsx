import { useEffect } from 'react'
import { motion } from 'framer-motion'
import site from '@/data/site'
import { HERO_PATHS } from '@/lib/images'
import { buildWhatsAppUrl } from '@/lib/whatsapp'
import { SafeImage } from '@/components/ui/SafeImage'
import { Button } from '@/components/ui/Button'

const notifyUrl = buildWhatsAppUrl(
  'Hi, I am interested in Bilal Textiles. Please notify me when your online catalogue launches.',
)

export function ComingSoonPage() {
  useEffect(() => {
    document.title = `${site.name} — Coming Soon`
  }, [])

  return (
    <div className="relative flex min-h-dvh flex-col overflow-hidden bg-obsidian text-inverse">
      <div className="absolute inset-0">
        <SafeImage
          src={HERO_PATHS[0]}
          fallback="/images/dresses/pro-3.webp"
          alt=""
          loading="eager"
          fetchPriority="high"
          className="h-full w-full object-cover object-center"
        />
        <div className="absolute inset-0 bg-gradient-to-b from-obsidian/88 via-obsidian/78 to-obsidian/92" />
        <div className="pointer-events-none absolute -right-24 top-1/4 h-80 w-80 rounded-full bg-brand/30 blur-[100px]" />
        <div className="pointer-events-none absolute -left-16 bottom-1/4 h-64 w-64 rounded-full bg-gold/15 blur-[80px]" />
      </div>

      <main className="relative z-10 flex flex-1 flex-col items-center justify-center px-margin-mobile py-16 text-center md:px-margin-desktop">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.7, ease: [0.22, 1, 0.36, 1] }}
          className="w-full max-w-xl"
        >
          <div className="mx-auto inline-flex items-center rounded-full border border-outline/45 bg-canvas-warm/92 px-5 py-3 shadow-soft ring-1 ring-black/[0.04] backdrop-blur-sm">
            <img
              src="/logo.png"
              alt={site.name}
              className="h-9 w-auto md:h-10"
            />
          </div>

          <p className="label-caps mt-10 text-brand-soft">{site.tagline}</p>

          <h1 className="mt-5 font-display text-4xl font-semibold leading-tight tracking-tight md:text-5xl lg:text-6xl">
            Coming Soon
          </h1>

          <p className="mx-auto mt-5 max-w-md text-base leading-relaxed text-inverse-muted md:text-lg">
            {site.slogan}. Our wholesale catalogue is being prepared ! Contact us for more information
            and bulk pricing.
          </p>

          <div className="mt-10 flex flex-col items-center justify-center gap-3 sm:flex-row sm:gap-4">
            <Button href={notifyUrl} variant="whatsapp" external className="w-full sm:w-auto">
              Contact us on WhatsApp
            </Button>
            <Button
              href={`tel:${site.phone.replace(/\s/g, '')}`}
              variant="outline"
              className="w-full border-inverse/30 text-inverse hover:border-brand hover:text-brand-soft sm:w-auto"
            >
              Contact us on Phone
            </Button>
          </div>

          <p className="mt-12 text-sm text-inverse-faint">Visit our store in Surat Textile Market or Hyderabad</p>
        </motion.div>
      </main>

      <footer className="relative z-10 border-t border-inverse/10 px-margin-mobile py-5 text-center text-xs text-inverse-faint md:px-margin-desktop">
        {site.copyright}
      </footer>
    </div>
  )
}
