import { useTransform, type MotionValue } from 'framer-motion'

/** Sub-pixel % transforms are a common source of scroll jitter — use rounded px instead. */
export function useParallaxPx(
  progress: MotionValue<number>,
  minPx: number,
  maxPx: number,
) {
  return useTransform(progress, (v) => Math.round(minPx + v * (maxPx - minPx)))
}

export function parallaxTransformTemplate({
  y,
}: {
  x?: string | number
  y?: string | number
  z?: string | number
}) {
  const yPx = typeof y === 'number' ? y : parseFloat(String(y ?? 0))
  return `translate3d(0, ${Number.isFinite(yPx) ? yPx : 0}px, 0)`
}
