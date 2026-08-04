import { Link } from 'react-router-dom'
import { getFeaturedCategories } from '@/lib/data'
import { SectionHeader } from '@/components/ui/SectionHeader'

export function CategoryBento() {
  const cats = getFeaturedCategories(4)
  const [a, b, c, d] = cats

  if (!a || !b || !c || !d) return null

  return (
    <section className="section-pad mx-auto max-w-container-max">
      <SectionHeader label="Catalog" title="Browse categories" viewAllHref="/salwar-kameez" />

      <div className="mt-8 grid grid-cols-2 gap-2 md:grid-cols-12 md:grid-rows-2 md:gap-3">
        <Link
          to={a.href}
          className="group relative col-span-2 row-span-2 min-h-[280px] overflow-hidden md:col-span-5 md:min-h-[480px]"
        >
          <img src={a.image} alt={a.title} className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105" />
          <div className="absolute inset-0 bg-linear-to-t from-canvas/80 via-transparent to-transparent" />
          <div className="absolute bottom-0 left-0 p-5">
            <p className="text-lg font-semibold text-ink">{a.title}</p>
            <p className="mt-1 text-sm text-ink-muted">{a.description}</p>
          </div>
        </Link>

        <Link
          to={b.href}
          className="group relative min-h-[160px] overflow-hidden md:col-span-4 md:min-h-0"
        >
          <img src={b.image} alt={b.title} className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105" />
          <div className="absolute inset-0 bg-canvas/30 transition-colors group-hover:bg-canvas/10" />
          <p className="absolute bottom-3 left-3 text-sm font-semibold text-ink">{b.title}</p>
        </Link>

        <Link
          to={c.href}
          className="group relative min-h-[160px] overflow-hidden md:col-span-3 md:min-h-0"
        >
          <img src={c.image} alt={c.title} className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105" />
          <div className="absolute inset-0 bg-canvas/30 transition-colors group-hover:bg-canvas/10" />
          <p className="absolute bottom-3 left-3 text-sm font-semibold text-ink">{c.title}</p>
        </Link>

        <Link
          to={d.href}
          className="group relative col-span-2 min-h-[120px] overflow-hidden md:col-span-7 md:min-h-0"
        >
          <img src={d.image} alt={d.title} className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105" />
          <div className="absolute inset-0 bg-linear-to-r from-canvas/70 to-transparent" />
          <p className="absolute bottom-3 left-3 text-sm font-semibold text-ink">{d.title}</p>
        </Link>
      </div>
    </section>
  )
}
