import site from '@/data/site'
import { generalEnquiryUrl } from '@/lib/whatsapp'
import { FadeIn } from '@/components/ui/FadeIn'

export function TradeCta() {
  return (
    <section className="relative overflow-hidden bg-obsidian py-24 md:py-32">
      <div
        className="absolute inset-0 opacity-[0.06]"
        style={{
          backgroundImage:
            'repeating-linear-gradient(45deg, #e9c176 0, #e9c176 1px, transparent 0, transparent 50%)',
          backgroundSize: '30px 30px',
        }}
      />
      <div className="relative mx-auto max-w-2xl px-margin-mobile md:px-margin-desktop">
        <FadeIn>
          <div className="rounded-2xl border border-outline/30 bg-surface p-8 shadow-elevated md:p-14">
            <h2 className="text-center font-display text-3xl text-ink md:text-4xl">
              Wholesale trade desk
            </h2>
            <p className="mt-4 text-center text-ink-muted">
              Bulk orders, catalogue access, and MOQ pricing — speak directly with our Surat team on
              WhatsApp.
            </p>
            <div className="mt-10 space-y-6">
              <div className="border-b border-outline/50 pb-3">
                <p className="label-caps text-ink-faint">Location</p>
                <p className="mt-2 text-sm text-ink-muted">{site.locationNote}</p>
              </div>
              <div className="border-b border-outline/50 pb-3">
                <p className="label-caps text-ink-faint">Phone</p>
                <a
                  href={`tel:${site.phone.replace(/\s/g, '')}`}
                  className="mt-2 block text-ink transition-colors hover:text-brand"
                >
                  {site.phone}
                </a>
              </div>
              <a
                href={generalEnquiryUrl()}
                target="_blank"
                rel="noopener noreferrer"
                className="label-caps flex w-full items-center justify-center bg-brand py-5 text-white transition-colors hover:bg-brand-dark"
              >
                Open WhatsApp enquiry
              </a>
            </div>
          </div>
        </FadeIn>
      </div>
    </section>
  )
}
