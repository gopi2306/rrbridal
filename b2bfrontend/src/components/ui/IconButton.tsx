import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { Tooltip } from './Tooltip'

interface IconButtonProps {
  to?: string
  href?: string
  onClick?: () => void
  label: string
  badge?: number
  children: ReactNode
  className?: string
  tooltipSide?: 'top' | 'bottom'
}

export function IconButton({
  to,
  href,
  onClick,
  label,
  badge,
  children,
  className = '',
  tooltipSide = 'bottom',
}: IconButtonProps) {
  const classes = `relative inline-flex h-10 w-10 cursor-pointer items-center justify-center rounded-full border border-white/15 text-white/80 transition-colors hover:border-white/30 hover:text-white ${className}`

  const badgeEl =
    badge !== undefined && badge > 0 ? (
      <span className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-brand px-1 text-[10px] font-bold text-white">
        {badge > 99 ? '99+' : badge}
      </span>
    ) : null

  const inner = (
    <>
      {children}
      {badgeEl}
    </>
  )

  const wrapped = (node: ReactNode) => (
    <Tooltip label={label} side={tooltipSide}>
      {node}
    </Tooltip>
  )

  if (to) {
    return wrapped(
      <Link to={to} aria-label={label} title={label} className={classes}>
        {inner}
      </Link>,
    )
  }

  if (href) {
    return wrapped(
      <a href={href} aria-label={label} title={label} className={classes}>
        {inner}
      </a>,
    )
  }

  return wrapped(
    <button type="button" onClick={onClick} aria-label={label} title={label} className={classes}>
      {inner}
    </button>,
  )
}
