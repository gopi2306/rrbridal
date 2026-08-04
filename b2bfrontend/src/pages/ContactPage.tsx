import site from '@/data/site'
import { PageHero } from '@/components/layout/PageHero'
import { FadeIn } from '@/components/ui/FadeIn'
import { Button } from '@/components/ui/Button'
import { generalEnquiryUrl } from '@/lib/whatsapp'

export function ContactPage() {
  const mapsUrl = `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(site.address)}`

  return (
    <>
      <PageHero
        title="Contact Us"
        subtitle="Reach out for wholesale enquiries, catalogue access, and bulk orders."
        breadcrumb={[{ label: 'Home', href: '/' }, { label: 'Contact' }]}
      />

      <section className="mx-auto max-w-7xl page-x py-12 md:py-16">
        <div className="grid gap-8 lg:grid-cols-3">
          <FadeIn>
            <div className="h-full rounded-3xl border border-ink/8 bg-surface p-6 shadow-soft sm:p-8">
              <h2 className="font-display text-2xl font-semibold text-ink">Visit Us</h2>
              <p className="mt-4 leading-relaxed text-ink-muted">{site.address}</p>
              <a
                href={mapsUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="mt-4 inline-block text-sm font-semibold text-brand hover:text-brand-dark"
              >
                Open in Google Maps →
              </a>
            </div>
          </FadeIn>

          <FadeIn delay={0.05}>
            <div className="h-full rounded-3xl border border-ink/8 bg-surface p-6 shadow-soft sm:p-8">
              <h2 className="font-display text-2xl font-semibold text-ink">Call</h2>
              <a
                href={`tel:${site.phone.replace(/\s/g, '')}`}
                className="mt-4 block text-xl font-semibold text-ink hover:text-brand"
              >
                {site.phone}
              </a>
              <p className="mt-3 text-sm text-ink-muted">Available for wholesale enquiries</p>
            </div>
          </FadeIn>

          <FadeIn delay={0.1}>
            <div className="h-full rounded-3xl bg-obsidian p-6 text-inverse sm:p-8">
              <h2 className="font-display text-2xl font-semibold">WhatsApp</h2>
              <p className="mt-4 text-inverse-muted">
                Fastest way to enquire about products, MOQ, and bulk pricing.
              </p>
              <Button href={generalEnquiryUrl()} variant="whatsapp" external className="mt-6">
                Chat on WhatsApp
              </Button>
            </div>
          </FadeIn>
        </div>
      </section>
    </>
  )
}
