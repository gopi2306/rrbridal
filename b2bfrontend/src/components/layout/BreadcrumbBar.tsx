import { Link } from 'react-router-dom'
import type { Breadcrumb } from '@/types'

interface BreadcrumbBarProps {
  items: Breadcrumb[]
}

export function BreadcrumbBar({ items }: BreadcrumbBarProps) {
  return (
    <nav
      aria-label="Breadcrumb"
      className="border-b border-ink/5 bg-canvas-warm/80 page-x py-3 text-sm text-ink-muted"
    >
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-2">
        {items.map((item, i) => (
          <span key={`${item.label}-${i}`} className="flex items-center gap-2">
            {i > 0 && <span className="text-ink-faint">/</span>}
            {item.href ? (
              <Link to={item.href} className="hover:text-brand">
                {item.label}
              </Link>
            ) : (
              <span className="line-clamp-1 text-ink">{item.label}</span>
            )}
          </span>
        ))}
      </div>
    </nav>
  )
}
