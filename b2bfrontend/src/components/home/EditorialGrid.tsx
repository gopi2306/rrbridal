import { Link } from 'react-router-dom'
import { FadeIn } from '@/components/ui/FadeIn'

export function EditorialGrid() {
  return (
    <section className="mx-auto max-w-container-max px-margin-mobile py-20 md:px-margin-desktop">
      <FadeIn className="mb-10">
        <p className="label-caps text-gold">Editorial</p>
        <h2 className="mt-3 font-display text-3xl text-ink md:text-4xl">The trade moodboard</h2>
      </FadeIn>

      <div className="grid auto-rows-[200px] grid-cols-2 gap-3 md:auto-rows-[320px] md:gap-gutter">
        <FadeIn className="group relative col-span-2 row-span-2 overflow-hidden">
          <Link to="/dress-material" className="block h-full">
            <img
              src="/images/dresses/pro-5.webp"
              alt="Dress materials wholesale"
              loading="lazy"
              className="h-full w-full object-cover transition-transform duration-1000 group-hover:scale-105"
            />
            <div className="absolute inset-0 bg-linear-to-t from-canvas/90 via-transparent to-transparent" />
            <h3 className="absolute bottom-4 left-4 font-display text-xl text-ink md:text-2xl">
              Raw materials
            </h3>
          </Link>
        </FadeIn>

        <FadeIn
          delay={0.05}
          className="col-span-1 row-span-1 flex items-center justify-center border border-outline/30 bg-surface p-6 text-center"
        >
          <p className="font-display text-2xl text-gold/70 md:text-4xl">
            &ldquo;Quality is the ultimate wholesale currency.&rdquo;
          </p>
        </FadeIn>

        <FadeIn delay={0.1} className="group relative col-span-1 row-span-2 overflow-hidden">
          <Link to="/premium" className="block h-full">
            <img
              src="/images/dresses/pro-11.webp"
              alt="Premium textile detail"
              loading="lazy"
              className="h-full w-full object-cover transition-transform duration-1000 group-hover:scale-105"
            />
            <div className="absolute inset-0 bg-linear-to-t from-canvas/90 via-transparent to-transparent" />
            <h3 className="absolute bottom-4 left-4 font-display text-xl text-ink md:text-2xl">
              Premium weave
            </h3>
          </Link>
        </FadeIn>

        <FadeIn delay={0.15} className="group relative col-span-1 row-span-1 overflow-hidden">
          <Link to="/readymade" className="block h-full">
            <img
              src="/images/dresses/pro-3.webp"
              alt="Readymade kurti collection"
              loading="lazy"
              className="h-full w-full object-cover transition-transform duration-1000 group-hover:scale-105"
            />
            <div className="absolute inset-0 bg-linear-to-t from-canvas/90 via-transparent to-transparent" />
            <h3 className="absolute bottom-4 left-4 font-display text-xl text-ink">Readymade</h3>
          </Link>
        </FadeIn>
      </div>
    </section>
  )
}
