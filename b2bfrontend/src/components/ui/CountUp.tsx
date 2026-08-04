import { useEffect, useRef, useState } from 'react'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

interface CountUpProps {
  value: string
  className?: string
  duration?: number
}

function formatCount(n: number): string {
  return n.toLocaleString('en-IN')
}

export function CountUp({ value, className, duration = 1.8 }: CountUpProps) {
  const reduced = usePrefersReducedMotion()
  const ref = useRef<HTMLSpanElement>(null)
  const [display, setDisplay] = useState(reduced ? value : '0')
  const startedRef = useRef(false)

  useEffect(() => {
    if (reduced) {
      setDisplay(value)
      return
    }

    const el = ref.current
    if (!el) return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting || startedRef.current) return
        startedRef.current = true

        if (value.includes('/')) {
          const [hours] = value.split('/')
          const hourTarget = parseInt(hours, 10)
          if (Number.isNaN(hourTarget)) {
            setDisplay(value)
            return
          }
          const start = performance.now()
          const run = (now: number) => {
            const progress = Math.min((now - start) / (duration * 1000), 1)
            const eased = 1 - (1 - progress) ** 3
            const current = Math.round(hourTarget * eased)
            setDisplay(`${current}/7`)
            if (progress < 1) requestAnimationFrame(run)
          }
          requestAnimationFrame(run)
          return
        }

        const match = value.match(/^([\d,]+)(.*)$/)
        if (!match) {
          setDisplay(value)
          return
        }

        const target = parseInt(match[1].replace(/,/g, ''), 10)
        const suffix = match[2] ?? ''
        const start = performance.now()

        const run = (now: number) => {
          const progress = Math.min((now - start) / (duration * 1000), 1)
          const eased = 1 - (1 - progress) ** 3
          const current = Math.round(target * eased)
          setDisplay(`${formatCount(current)}${suffix}`)
          if (progress < 1) requestAnimationFrame(run)
        }

        requestAnimationFrame(run)
      },
      { threshold: 0.35 },
    )

    observer.observe(el)
    return () => observer.disconnect()
  }, [value, duration, reduced])

  return (
    <span ref={ref} className={className}>
      {display}
    </span>
  )
}
