import { Link } from 'react-router-dom'

interface SectionHeaderProps {
  label: string
  title: string
  viewAllHref?: string
  viewAllLabel?: string
  className?: string
  bordered?: boolean
}

export function SectionHeader({
  label,
  title,
  viewAllHref,
  viewAllLabel = 'View all',
  className = '',
  bordered = true,
}: SectionHeaderProps) {
  return (
    <div
      className={`flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between sm:gap-4 ${bordered ? 'border-b border-outline pb-4' : ''} ${className}`.trim()}
    >
      <div>
        <p className="label-caps text-ink-faint">{label}</p>
        <h2 className="mt-2 text-xl font-semibold tracking-tight text-ink sm:text-2xl md:text-3xl">{title}</h2>
      </div>
      {viewAllHref && (
        <Link
          to={viewAllHref}
          className="label-caps shrink-0 text-ink-muted transition-colors hover:text-brand"
        >
          {viewAllLabel} →
        </Link>
      )}
    </div>
  )
}
