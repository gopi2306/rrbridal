import { useState, type ReactNode } from 'react'

interface FilterSectionProps {
  title: string
  children: ReactNode
  defaultOpen?: boolean
}

export function FilterSection({ title, children, defaultOpen = true }: FilterSectionProps) {
  const [open, setOpen] = useState(defaultOpen)

  return (
    <div className="border-b border-ink/8 py-3 last:border-b-0">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="flex w-full cursor-pointer items-center justify-between rounded-lg px-1 py-1.5 text-left text-sm font-semibold text-ink transition-colors hover:bg-canvas-warm/70"
        aria-expanded={open}
      >
        {title}
        <span
          className={`flex h-6 w-6 items-center justify-center rounded-full text-sm leading-none text-ink-faint transition-transform ${
            open ? 'rotate-0' : ''
          }`}
          aria-hidden
        >
          {open ? '−' : '+'}
        </span>
      </button>
      {open ? <div className="mt-2 space-y-0.5 pb-1">{children}</div> : null}
    </div>
  )
}
