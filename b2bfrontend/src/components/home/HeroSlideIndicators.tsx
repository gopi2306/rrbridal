import { motion } from 'framer-motion'
import type { HeroSlide } from '@/lib/images'

interface HeroSlideIndicatorsProps {
  slides: HeroSlide[]
  index: number
  onSelect: (i: number) => void
  reducedMotion: boolean
  variant?: 'light' | 'inverse'
  showCount?: boolean
}

export function HeroSlideIndicators({
  slides,
  index,
  onSelect,
  reducedMotion,
  variant = 'light',
  showCount = true,
}: HeroSlideIndicatorsProps) {
  const inverse = variant === 'inverse'
  const spring = reducedMotion
    ? { duration: 0 }
    : { type: 'spring' as const, stiffness: 380, damping: 30 }

  return (
    <div className="flex items-center gap-3" role="tablist" aria-label="Hero slides">
      {showCount && (
        <span
          className={`label-caps hidden tabular-nums sm:inline ${inverse ? 'text-inverse-faint' : 'text-ink-faint'}`}
        >
          {String(index + 1).padStart(2, '0')}
          <span className={`mx-1.5 ${inverse ? 'text-inverse/25' : 'text-ink/20'}`}>/</span>
          {String(slides.length).padStart(2, '0')}
        </span>
      )}

      <div className="flex items-center gap-2">
        {slides.map((s, i) => {
          const isActive = i === index
          return (
            <button
              key={s.src}
              type="button"
              role="tab"
              aria-selected={isActive}
              aria-label={s.label}
              onClick={() => onSelect(i)}
              className="group relative flex h-8 items-center px-1"
            >
              {isActive ? (
                <motion.span
                  layoutId="hero-indicator-pill"
                  className="block h-1.5 w-10 rounded-full bg-brand sm:w-12"
                  transition={spring}
                />
              ) : (
                <span
                  className={`block h-1.5 w-2 rounded-full transition-colors sm:w-2.5 ${
                    inverse
                      ? 'bg-inverse/35 group-hover:bg-inverse/55'
                      : 'bg-ink/20 group-hover:bg-ink/40'
                  }`}
                />
              )}
            </button>
          )
        })}
      </div>
    </div>
  )
}
