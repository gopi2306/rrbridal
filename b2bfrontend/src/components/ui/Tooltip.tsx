import {
  useCallback,
  useEffect,
  useId,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
} from 'react'
import { createPortal } from 'react-dom'

type Side = 'top' | 'bottom'
type SidePreference = Side | 'auto'

interface TooltipProps {
  label: string
  children: ReactNode
  side?: SidePreference
  nowrap?: boolean
}

const GAP = 8
const VIEWPORT_PAD = 10

function pickSide(rect: DOMRect, tipHeight: number, preference: SidePreference): Side {
  const spaceAbove = rect.top - VIEWPORT_PAD
  const spaceBelow = window.innerHeight - rect.bottom - VIEWPORT_PAD
  const need = tipHeight + GAP

  if (preference === 'top') {
    return spaceAbove >= need || spaceAbove >= spaceBelow ? 'top' : 'bottom'
  }
  if (preference === 'bottom') {
    return spaceBelow >= need || spaceBelow >= spaceAbove ? 'bottom' : 'top'
  }

  return spaceBelow >= need || spaceBelow >= spaceAbove ? 'bottom' : 'top'
}

export function Tooltip({ label, children, side = 'auto', nowrap = true }: TooltipProps) {
  const tooltipId = useId()
  const anchorRef = useRef<HTMLSpanElement>(null)
  const tipRef = useRef<HTMLSpanElement>(null)
  const [open, setOpen] = useState(false)
  const [ready, setReady] = useState(false)
  const [placement, setPlacement] = useState<Side>('bottom')
  const [coords, setCoords] = useState<CSSProperties>({
    position: 'fixed',
    top: -9999,
    left: -9999,
  })

  const positionTooltip = useCallback(() => {
    const anchor = anchorRef.current
    const tip = tipRef.current
    if (!anchor || !tip) return

    const rect = anchor.getBoundingClientRect()
    const tipRect = tip.getBoundingClientRect()
    const nextSide = pickSide(rect, tipRect.height, side)

    const centerX = rect.left + rect.width / 2
    let left = centerX - tipRect.width / 2
    left = Math.max(VIEWPORT_PAD, Math.min(left, window.innerWidth - tipRect.width - VIEWPORT_PAD))

    const top =
      nextSide === 'top' ? rect.top - tipRect.height - GAP : rect.bottom + GAP

    setPlacement(nextSide)
    setCoords({
      position: 'fixed',
      top: Math.max(VIEWPORT_PAD, Math.min(top, window.innerHeight - tipRect.height - VIEWPORT_PAD)),
      left,
    })
    setReady(true)
  }, [side])

  const show = useCallback(() => {
    setOpen(true)
    setReady(false)
  }, [])

  const hide = useCallback(() => {
    setOpen(false)
    setReady(false)
  }, [])

  useEffect(() => {
    if (!open) return

    const frame = requestAnimationFrame(() => {
      positionTooltip()
    })

    const onReflow = () => positionTooltip()
    window.addEventListener('resize', onReflow, { passive: true })
    window.addEventListener('scroll', onReflow, { passive: true, capture: true })

    return () => {
      cancelAnimationFrame(frame)
      window.removeEventListener('resize', onReflow)
      window.removeEventListener('scroll', onReflow, { capture: true })
    }
  }, [open, label, positionTooltip])

  const motion =
    placement === 'top'
      ? ready
        ? 'translate-y-0 opacity-100'
        : 'translate-y-1 opacity-0'
      : ready
        ? 'translate-y-0 opacity-100'
        : '-translate-y-1 opacity-0'

  return (
    <>
      <span
        ref={anchorRef}
        className="inline-flex"
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocusCapture={show}
        onBlurCapture={(e) => {
          if (!e.currentTarget.contains(e.relatedTarget as Node)) hide()
        }}
        aria-describedby={open ? tooltipId : undefined}
      >
        {children}
      </span>

      {open &&
        createPortal(
          <span
            ref={tipRef}
            id={tooltipId}
            role="tooltip"
            style={coords}
            className={`pointer-events-none z-[9999] rounded-md bg-obsidian px-2.5 py-1.5 text-xs font-medium text-inverse shadow-md transition-[opacity,transform] duration-200 ease-out ${motion} ${nowrap ? 'whitespace-nowrap leading-none' : 'max-w-[10rem] text-center leading-snug'}`}
          >
            {label}
          </span>,
          document.body,
        )}
    </>
  )
}
