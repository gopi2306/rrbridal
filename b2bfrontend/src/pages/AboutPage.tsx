import { about as content } from '@/data/about'
import { PageHero } from '@/components/layout/PageHero'
import { FadeIn, Stagger, StaggerItem } from '@/components/ui/FadeIn'
import { getHeroImage } from '@/lib/images'

export function AboutPage() {
  return (
    <>
      <PageHero
        title="About Us"
        subtitle="Your trusted partner for wholesale ethnic wear from Surat Textile Market."
        breadcrumb={[{ label: 'Home', href: '/' }, { label: 'About Us' }]}
      />

      <section className="mx-auto max-w-7xl page-x py-16">
        <div className="grid items-center gap-8 lg:grid-cols-2 lg:gap-12">
          <FadeIn>
            <h2 className="font-display text-3xl font-semibold text-ink sm:text-4xl">Who We Are</h2>
            <p className="mt-5 text-lg leading-relaxed text-ink-muted">{content.intro}</p>
            <p className="mt-4 leading-relaxed text-ink-muted">{content.mission}</p>
          </FadeIn>
          <FadeIn delay={0.1}>
            <div className="overflow-hidden rounded-[2rem] shadow-elevated">
              <img src={getHeroImage()} alt="Bilal Textiles collection" className="aspect-[4/5] w-full object-cover" />
            </div>
          </FadeIn>
        </div>

        <Stagger className="mt-16 grid grid-cols-2 gap-4 sm:gap-6 md:mt-20 md:grid-cols-4">
          {content.highlights.map((item) => (
            <StaggerItem key={item.title}>
              <div className="rounded-3xl border border-ink/8 bg-surface p-4 text-center shadow-soft sm:p-6">
                <p className="font-display text-2xl font-semibold text-brand sm:text-3xl md:text-4xl">{item.title}</p>
                <p className="mt-2 text-sm text-ink-muted">{item.desc}</p>
              </div>
            </StaggerItem>
          ))}
        </Stagger>

        <div className="mt-24">
          <FadeIn>
            <h2 className="text-center font-display text-3xl font-semibold text-ink sm:text-4xl">Why Bilal Textiles</h2>
          </FadeIn>
          <Stagger className="mt-12 grid gap-6 md:grid-cols-2">
            {content.values.map((value) => (
              <StaggerItem key={value.title}>
                <div className="h-full rounded-3xl border border-ink/8 bg-canvas-warm/50 p-6 sm:p-8">
                  <h3 className="font-display text-xl font-semibold text-ink sm:text-2xl">{value.title}</h3>
                  <p className="mt-3 leading-relaxed text-ink-muted">{value.desc}</p>
                </div>
              </StaggerItem>
            ))}
          </Stagger>
        </div>
      </section>
    </>
  )
}
