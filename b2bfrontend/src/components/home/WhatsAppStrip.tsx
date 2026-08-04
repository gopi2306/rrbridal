import site from '@/data/site'
import { generalEnquiryUrl } from '@/lib/whatsapp'

export function WhatsAppStrip() {
  return (
    <section className="border-t border-outline bg-surface">
      <div className="mx-auto flex max-w-container-max flex-col items-center justify-between gap-4 px-margin-mobile py-8 md:flex-row md:px-margin-desktop">
        <div>
          <p className="text-sm font-semibold text-ink">Bulk orders &amp; line sheets</p>
          <p className="mt-1 text-sm text-ink-muted">{site.tagline} · {site.phone}</p>
        </div>
        <a
          href={generalEnquiryUrl()}
          target="_blank"
          rel="noopener noreferrer"
          className="w-full text-center label-caps bg-brand px-4 py-3 text-xs text-white hover:bg-brand-dark sm:w-auto sm:px-8"
        >
          WhatsApp trade desk
        </a>
      </div>
    </section>
  )
}
