import { Link } from 'react-router-dom'
import { FadeIn } from '@/components/ui/FadeIn'
import type { Breadcrumb } from '@/types'

interface PageHeroProps {
  title: string
  subtitle?: string
  breadcrumb?: Breadcrumb[]
}

export function PageHero({ title, subtitle, breadcrumb }: PageHeroProps) {
  return (
    <section className="relative overflow-hidden border-b border-outline/30 bg-canvas-warm">
      <div className="absolute -right-20 top-0 h-64 w-64 rounded-full bg-brand/10 blur-3xl" />
      <div className="mx-auto max-w-7xl page-x py-12 md:py-20">
        {breadcrumb && (
          <FadeIn>
            <nav className="mb-6 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
              {breadcrumb.map((item, i) => (
                <span key={item.label} className="flex items-center gap-2">
                  {i > 0 && <span className="text-ink-faint">/</span>}
                  {item.href ? (
                    <Link to={item.href} className="hover:text-brand">
                      {item.label}
                    </Link>
                  ) : (
                    <span className="text-ink">{item.label}</span>
                  )}
                </span>
              ))}
            </nav>
          </FadeIn>
        )}
        <FadeIn delay={0.05}>
          <h1 className="font-display text-3xl font-semibold text-ink sm:text-4xl md:text-5xl lg:text-6xl">{title}</h1>
        </FadeIn>
        {subtitle && (
          <FadeIn delay={0.1}>
            <p className="mt-4 max-w-2xl text-base text-ink-muted md:text-lg">{subtitle}</p>
          </FadeIn>
        )}
      </div>
    </section>
  )
}
