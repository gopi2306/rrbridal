import { testimonials as items } from '@/data/testimonials'
import { SectionHeading } from '@/components/ui/SectionHeading'
import { FadeIn, Stagger, StaggerItem } from '@/components/ui/FadeIn'

export function Testimonials() {
  return (
    <section className="mx-auto max-w-7xl px-4 py-24">
      <SectionHeading
        eyebrow="Testimonials"
        title="What Our Customers Say"
        subtitle="Trusted by wholesalers across India and abroad."
        align="center"
      />

      <Stagger className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        {items.map((item) => (
          <StaggerItem key={item.id}>
            <FadeIn>
              <blockquote className="flex h-full flex-col rounded-3xl border border-ink/8 bg-surface p-8 shadow-soft">
                <div className="mb-4 flex gap-1 text-gold" aria-hidden>
                  {'★★★★★'.split('').map((star, i) => (
                    <span key={i}>{star}</span>
                  ))}
                </div>
                <p className="flex-1 text-sm leading-relaxed text-ink-muted">&ldquo;{item.text}&rdquo;</p>
                <footer className="mt-6 border-t border-ink/8 pt-4">
                  <cite className="not-italic font-semibold text-ink">{item.name}</cite>
                </footer>
              </blockquote>
            </FadeIn>
          </StaggerItem>
        ))}
      </Stagger>
    </section>
  )
}
