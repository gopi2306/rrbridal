import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'

type Variant = 'primary' | 'secondary' | 'ghost' | 'whatsapp' | 'outline'

interface ButtonProps {
  children: ReactNode
  variant?: Variant
  href?: string
  to?: string
  onClick?: () => void
  className?: string
  external?: boolean
  disabled?: boolean
}

const variants: Record<Variant, string> = {
  primary: 'bg-brand text-white hover:bg-brand-dark',
  secondary: 'bg-ink text-canvas hover:bg-ink-muted',
  ghost: 'border border-outline text-ink-muted hover:border-brand hover:text-brand',
  whatsapp: 'bg-[#25D366] text-white hover:bg-[#1fb855]',
  outline: 'border border-ink/30 text-ink hover:border-brand hover:text-brand',
}

export function Button({
  children,
  variant = 'primary',
  href,
  to,
  onClick,
  className = '',
  external,
  disabled,
}: ButtonProps) {
  const base =
    'inline-flex cursor-pointer items-center justify-center gap-2 px-6 py-3 text-sm font-medium transition-colors duration-200 disabled:cursor-not-allowed disabled:opacity-50'

  const classes = `${base} ${variants[variant]} ${className}`

  if (to) {
    return (
      <Link to={to} className={classes}>
        {children}
      </Link>
    )
  }

  if (href) {
    return (
      <a
        href={href}
        className={classes}
        target={external ? '_blank' : undefined}
        rel={external ? 'noopener noreferrer' : undefined}
        onClick={onClick}
      >
        {children}
      </a>
    )
  }

  return (
    <button type="button" className={classes} onClick={onClick} disabled={disabled}>
      {children}
    </button>
  )
}
