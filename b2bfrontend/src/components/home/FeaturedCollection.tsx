import { Link } from 'react-router-dom'
import { FadeIn } from '@/components/ui/FadeIn'
import { Button } from '@/components/ui/Button'
import { formatPrice, getProductsByFeatured } from '@/lib/data'
import { getProductImage, getHeroImage } from '@/lib/images'
import { productPath } from '@/lib/api'
import { titleCase } from '@/lib/format'

export function FeaturedCollection() {
  const hero = getProductsByFeatured('summer')[0]

  return (
    <section className="mx-auto max-w-container-max px-margin-mobile py-20 md:px-margin-desktop md:py-28">
      <div className="grid items-center gap-gutter overflow-hidden border border-outline/30 bg-surface lg:grid-cols-2">
        <FadeIn className="relative min-h-[300px] lg:min-h-[520px]">
          <img
            src={getHeroImage()}
            alt="Summer special collection"
            loading="lazy"
            className="absolute inset-0 h-full w-full object-cover"
          />
          <div className="absolute inset-0 bg-linear-to-r from-transparent to-surface/80 lg:to-surface" />
        </FadeIn>

        <FadeIn delay={0.08} className="px-8 py-12 lg:px-12 lg:py-16">
          <p className="label-caps text-brand">Shop by collection</p>
          <h2 className="mt-4 font-display text-4xl leading-tight text-ink md:text-5xl">
            Summer Special Collection
          </h2>
          <p className="mt-5 text-lg leading-relaxed text-ink-muted">
            Fresh kurti sets, co-ords, and designer suits at factory wholesale prices — built for
            retailers stocking the season&apos;s most-requested silhouettes.
          </p>
          {hero && (
            <div className="mt-8 flex items-center gap-4 border border-outline/40 bg-canvas-warm p-4">
              <img
                src={getProductImage(hero)}
                alt={hero.name}
                className="h-20 w-16 shrink-0 object-cover"
              />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-ink">{titleCase(hero.name)}</p>
                <p className="text-sm tabular-nums text-gold">
                  MOQ {hero.moq} pcs · {formatPrice(hero.price)}
                </p>
              </div>
            </div>
          )}
          <div className="mt-8 flex flex-wrap items-center gap-4">
            <Button to="/readymade" variant="primary">
              Shop collection
            </Button>
            {hero && (
              <Link
                to={productPath(hero)}
                className="label-caps text-gold transition-colors hover:text-brand"
              >
                View featured piece →
              </Link>
            )}
          </div>
        </FadeIn>
      </div>
    </section>
  )
}
