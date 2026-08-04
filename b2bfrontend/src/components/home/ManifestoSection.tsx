import { Link } from 'react-router-dom'
import { FadeIn } from '@/components/ui/FadeIn'
import site from '@/data/site'

export function ManifestoSection() {
  return (
    <section className="mx-auto max-w-container-max px-margin-mobile py-24 md:px-margin-desktop md:py-32">
      <div className="grid grid-cols-1 gap-gutter md:grid-cols-12">
        <FadeIn className="md:col-span-4 md:flex md:flex-col md:justify-end md:pb-4">
          <span className="label-caps flex items-center gap-4 text-brand">
            <span className="h-px w-8 bg-brand" />
            Wholesale · Surat
          </span>
        </FadeIn>

        <FadeIn delay={0.08} className="md:col-span-8">
          <h2 className="font-display text-3xl font-light leading-tight text-ink md:text-5xl">
            Factory-direct ethnic wear for retailers who need{' '}
            <span className="text-maroon italic">consistent reorders</span>. Based in Surat Textile
            Market, {site.name} supplies salwar suits, kurtis, and bridal stock at trade MOQ.
          </h2>
          <Link
            to="/about"
            className="label-caps mt-12 inline-flex border border-maroon/30 px-8 py-4 text-maroon transition-all duration-500 hover:bg-maroon hover:text-white"
          >
            About our wholesale
          </Link>
        </FadeIn>
      </div>
    </section>
  )
}
