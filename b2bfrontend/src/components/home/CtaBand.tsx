import { FadeIn } from '@/components/ui/FadeIn'
import { Button } from '@/components/ui/Button'
import { generalEnquiryUrl } from '@/lib/whatsapp'

export function CtaBand() {
  return (
    <section className="mx-4 mb-24">
      <FadeIn>
        <div className="relative mx-auto max-w-7xl overflow-hidden rounded-[2rem] bg-ink px-8 py-16 text-center md:px-16">
          <div className="absolute -left-10 top-0 h-40 w-40 rounded-full bg-brand/30 blur-3xl" />
          <div className="absolute -right-10 bottom-0 h-40 w-40 rounded-full bg-gold/20 blur-3xl" />

          <p className="relative text-xs font-semibold uppercase tracking-[0.25em] text-gold">Wholesale Enquiry</p>
          <h2 className="relative mt-4 font-display text-4xl font-semibold text-white md:text-5xl">
            Ready to stock Bilal Textiles?
          </h2>
          <p className="relative mx-auto mt-4 max-w-xl text-white/70">
            Message us on WhatsApp for catalogue access, MOQ details, and bulk pricing.
          </p>
          <div className="relative mt-8 flex flex-wrap justify-center gap-4">
            <Button href={generalEnquiryUrl()} variant="whatsapp" external>
              Enquire on WhatsApp
            </Button>
            <Button to="/contact" variant="ghost" className="border-white/25 text-white hover:border-white hover:text-white">
              Contact Details
            </Button>
          </div>
        </div>
      </FadeIn>
    </section>
  )
}
