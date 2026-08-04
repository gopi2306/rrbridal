import { useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { testimonials } from '@/data/testimonials'
import { FadeIn } from '@/components/ui/FadeIn'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

export function TestimonialsCarousel() {
  const [index, setIndex] = useState(0)
  const reduced = usePrefersReducedMotion()
  const current = testimonials[index]

  const next = () => setIndex((i) => (i + 1) % testimonials.length)
  const prev = () => setIndex((i) => (i - 1 + testimonials.length) % testimonials.length)

  return (
    <section className="border-y border-outline/30 py-24 md:py-28">
      <div className="mx-auto max-w-4xl px-margin-mobile md:px-margin-desktop">
        <FadeIn className="mb-10 text-center">
          <p className="label-caps text-gold">Testimonials</p>
          <h2 className="mt-3 font-display text-3xl text-ink md:text-4xl">Trusted by wholesalers</h2>
        </FadeIn>

        <FadeIn>
          <div className="border border-outline/40 bg-surface p-8 md:p-12">
            <div className="flex justify-center gap-1 text-gold" aria-hidden>
              {'★★★★★'.split('').map((s, i) => (
                <span key={i}>{s}</span>
              ))}
            </div>

            <div className="relative mt-6 h-[280px] md:h-[240px]">
              <AnimatePresence mode="wait">
                <motion.blockquote
                  key={current.id}
                  initial={reduced ? false : { opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={reduced ? undefined : { opacity: 0 }}
                  transition={{ duration: 0.3 }}
                  className="absolute inset-0 flex flex-col items-center justify-center px-2 text-center"
                >
                  <p className="line-clamp-6 max-w-2xl text-base leading-relaxed text-ink-muted md:text-lg">
                    &ldquo;{current.text}&rdquo;
                  </p>
                  <footer className="mt-5 shrink-0 font-display text-lg font-semibold text-ink md:text-xl">
                    — {current.name}
                  </footer>
                </motion.blockquote>
              </AnimatePresence>
            </div>

            <div className="mt-8 flex items-center justify-center gap-4 border-t border-outline/30 pt-8">
              <button
                type="button"
                onClick={prev}
                className="flex h-10 w-10 shrink-0 items-center justify-center border border-outline/50 text-ink-muted transition-colors hover:border-gold hover:text-gold"
                aria-label="Previous testimonial"
              >
                ‹
              </button>
              <div className="flex gap-2">
                {testimonials.map((t, i) => (
                  <button
                    key={t.id}
                    type="button"
                    onClick={() => setIndex(i)}
                    aria-label={`Go to testimonial ${i + 1}`}
                    className={`h-2 rounded-full transition-all ${i === index ? 'w-8 bg-brand' : 'w-2 bg-outline/80'}`}
                  />
                ))}
              </div>
              <button
                type="button"
                onClick={next}
                className="flex h-10 w-10 shrink-0 items-center justify-center border border-outline/50 text-ink-muted transition-colors hover:border-gold hover:text-gold"
                aria-label="Next testimonial"
              >
                ›
              </button>
            </div>
          </div>
        </FadeIn>
      </div>
    </section>
  )
}
