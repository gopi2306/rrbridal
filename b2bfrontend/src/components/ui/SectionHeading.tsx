import { FadeIn } from './FadeIn'

interface SectionHeadingProps {
  eyebrow?: string
  title: string
  subtitle?: string
  align?: 'left' | 'center'
}

export function SectionHeading({
  eyebrow,
  title,
  subtitle,
  align = 'left',
}: SectionHeadingProps) {
  const alignClass = align === 'center' ? 'text-center mx-auto' : ''

  return (
    <FadeIn className={`mb-12 max-w-2xl ${alignClass}`}>
      {eyebrow && (
        <p className="mb-3 text-xs font-semibold uppercase tracking-[0.25em] text-brand">
          {eyebrow}
        </p>
      )}
      <h2 className="font-display text-4xl font-semibold leading-tight text-ink md:text-5xl">
        {title}
      </h2>
      {subtitle && (
        <p className="mt-4 text-lg leading-relaxed text-ink-muted">{subtitle}</p>
      )}
    </FadeIn>
  )
}
