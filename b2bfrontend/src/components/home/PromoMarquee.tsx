import site from '@/data/site'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

const ITEMS = [
  site.tagline,
  'MOQ from 4 pcs',
  site.locationNote,
  'Factory-direct wholesale',
  'Daily new stock',
  'WhatsApp trade desk',
]

function MarqueeContent() {
  return (
    <>
      {ITEMS.map((item) => (
        <span key={item} className="flex shrink-0 items-center gap-8">
          <span>{item}</span>
          <span className="text-brand" aria-hidden>
            ✦
          </span>
        </span>
      ))}
    </>
  )
}

export function PromoMarquee() {
  const reduced = usePrefersReducedMotion()

  return (
    <div className="overflow-hidden border-b border-outline bg-brand-soft">
      <div
        className={`flex w-max gap-8 px-margin-mobile py-2.5 md:px-margin-desktop ${reduced ? '' : 'marquee-track'}`}
      >
        <p className="label-caps flex shrink-0 items-center gap-8 text-[10px] text-maroon sm:text-xs">
          <MarqueeContent />
        </p>
        {!reduced && (
          <p className="label-caps flex shrink-0 items-center gap-8 text-[10px] text-maroon sm:text-xs" aria-hidden>
            <MarqueeContent />
          </p>
        )}
      </div>
    </div>
  )
}
