import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import site from '@/data/site'
import { useShop } from '@/context/ShopContext'
import { SiteMenu } from '@/components/layout/SiteMenu'

function CartIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden>
      <path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z" />
      <path d="M3 6h18" />
      <path d="M16 10a4 4 0 0 1-8 0" />
    </svg>
  )
}

function MenuIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden>
      <path d="M4 7h16M4 12h16M4 17h10" />
    </svg>
  )
}

const DOCK =
  'rounded-full border border-outline/60 bg-canvas/82 shadow-soft ring-1 ring-black/[0.03] backdrop-blur-md transition-[background-color,box-shadow,border-color] duration-300'

export function Header() {
  const chromeRef = useRef<HTMLDivElement>(null)
  const [menuOpen, setMenuOpen] = useState(false)
  const [scrolled, setScrolled] = useState(false)
  const { cartCount } = useShop()
  const location = useLocation()

  useEffect(() => {
    setMenuOpen(false)
  }, [location.pathname])

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 20)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  useEffect(() => {
    const syncHeight = () => {
      const top = 16
      const pill = chromeRef.current?.querySelector('[data-nav-pill]') as HTMLElement | null
      const pillH = pill?.offsetHeight ?? 40
      document.documentElement.style.setProperty('--header-height', `${top + pillH + 12}px`)
    }

    syncHeight()
    const observer = new ResizeObserver(syncHeight)
    if (chromeRef.current) observer.observe(chromeRef.current)
    window.addEventListener('resize', syncHeight, { passive: true })

    return () => {
      observer.disconnect()
      window.removeEventListener('resize', syncHeight)
    }
  }, [])

  const dockSolid = scrolled || menuOpen

  return (
    <>
      <div ref={chromeRef} className="pointer-events-none fixed inset-x-0 top-0 z-50 h-0">
        <Link
          to="/"
          data-nav-pill
          className={`pointer-events-auto fixed left-4 top-4 flex items-center gap-2.5 px-3 py-2 sm:left-6 sm:px-4 ${DOCK} ${
            dockSolid ? 'border-outline/80 bg-surface/92 shadow-elevated' : ''
          }`}
        >
          <img src="/logo.png" alt={site.name} className="h-7 w-auto sm:h-8" />
          {/* <span className="hidden font-display text-sm font-medium tracking-tight text-ink sm:inline">
            {site.name.split(' ')[0]}
          </span> */}
        </Link>

        <div
          className={`pointer-events-auto fixed right-4 top-4 flex items-center gap-0.5 p-1 sm:right-6 ${DOCK} ${
            dockSolid ? 'border-outline/80 bg-surface/92 shadow-elevated' : ''
          }`}
        >
          <button
            type="button"
            onClick={() => setMenuOpen(true)}
            className="flex items-center gap-2 rounded-full px-3 py-2 text-ink transition-colors hover:text-brand"
            aria-expanded={menuOpen}
            aria-haspopup="dialog"
          >
            <MenuIcon />
            <span className="label-caps text-[10px] sm:text-xs">Menu</span>
          </button>

          <span className="mx-0.5 h-5 w-px bg-outline/70" aria-hidden />

          <Link
            to="/cart"
            className="relative flex h-9 w-9 items-center justify-center rounded-full text-ink-muted transition-colors hover:text-brand"
            aria-label={cartCount > 0 ? `Cart, ${cartCount} items` : 'Cart'}
          >
            <CartIcon />
            {cartCount > 0 && (
              <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-brand px-1 text-[10px] font-bold text-white">
                {cartCount > 99 ? '99+' : cartCount}
              </span>
            )}
          </Link>
        </div>
      </div>

      <SiteMenu open={menuOpen} onClose={() => setMenuOpen(false)} />
    </>
  )
}
