import { useEffect, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { AnimatePresence, motion, type Variants } from 'framer-motion'
import site from '@/data/site'
import { categories } from '@/lib/data'
import { generalEnquiryUrl } from '@/lib/whatsapp'
import { useShop } from '@/context/ShopContext'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'
import { Tooltip } from '@/components/ui/Tooltip'
import { useCatalog } from '@/context/CatalogContext'

interface SiteMenuProps {
  open: boolean
  onClose: () => void
}

const PAGE_LINKS = [
  { to: '/', label: 'Home', hint: 'Start here' },
  { to: '/about', label: 'About', hint: 'Our wholesale story' },
  { to: '/premium', label: 'Bridal', hint: 'Premium & occasion' },
  { to: '/contact', label: 'Contact', hint: 'Trade enquiries' },
] as const

const panel: Variants = {
  hidden: { opacity: 0 },
  show: { opacity: 1, transition: { duration: 0.28 } },
  exit: { opacity: 0, transition: { duration: 0.2 } },
}

function MenuIconBtn({
  to,
  href,
  onClick,
  label,
  count,
  children,
  external,
}: {
  to?: string
  href?: string
  onClick?: () => void
  label: string
  count?: number
  children: ReactNode
  external?: boolean
}) {
  const className =
    'relative flex h-9 w-9 items-center justify-center rounded-full text-ink-muted transition-colors hover:bg-canvas-warm hover:text-brand'

  const badge =
    count !== undefined && count > 0 ? (
      <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-brand px-1 text-[9px] font-bold text-white">
        {count > 99 ? '99+' : count}
      </span>
    ) : null

  const inner = (
    <>
      {children}
      {badge}
    </>
  )

  const tip = (node: ReactNode) => (
    <Tooltip label={label}>{node}</Tooltip>
  )

  if (href) {
    return tip(
      <a
        href={href}
        target={external ? '_blank' : undefined}
        rel={external ? 'noopener noreferrer' : undefined}
        aria-label={label}
        onClick={onClick}
        className={className}
      >
        {inner}
      </a>,
    )
  }

  if (to) {
    return tip(
      <Link to={to} aria-label={label} onClick={onClick} className={className}>
        {inner}
      </Link>,
    )
  }

  return tip(
    <button type="button" aria-label={label} onClick={onClick} className={className}>
      {inner}
    </button>,
  )
}

const collectionGrid: Variants = {
  hidden: {},
  show: { transition: { staggerChildren: 0.05, delayChildren: 0.12 } },
}

const collectionCard: Variants = {
  hidden: { opacity: 0, y: 12 },
  show: { opacity: 1, y: 0, transition: { duration: 0.4, ease: [0.22, 1, 0.36, 1] } },
}

function CartIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden>
      <path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z" />
      <path d="M3 6h18" />
      <path d="M16 10a4 4 0 0 1-8 0" />
    </svg>
  )
}

function HeartIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden>
      <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
    </svg>
  )
}

function UserIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden>
      <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
      <circle cx="12" cy="7" r="4" />
    </svg>
  )
}

function UserPlusIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden>
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M19 8v6M22 11h-6" />
    </svg>
  )
}

function WhatsAppIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="currentColor" className="text-[#25D366]" aria-hidden>
      <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347z" />
      <path d="M12 0C5.373 0 0 5.373 0 12c0 2.123.558 4.117 1.532 5.84L0 24l6.324-1.658A11.95 11.95 0 0012 24c6.627 0 12-5.373 12-12S18.627 0 12 0zm0 21.82a9.78 9.78 0 01-4.99-1.37l-.358-.213-3.75.982 1.001-3.648-.233-.374A9.782 9.782 0 012.18 12C2.18 6.57 6.57 2.18 12 2.18S21.82 6.57 21.82 12 17.43 21.82 12 21.82z" />
    </svg>
  )
}

export function SiteMenu({ open, onClose }: SiteMenuProps) {
  const reduced = usePrefersReducedMotion()
  const { profile, clearProfile, cartCount, wishlistCount } = useShop()
  const { categories: liveCategories } = useCatalog()
  const collectionCategories = liveCategories.length > 0
    ? liveCategories.map((category, index) => {
        const staticCategory = categories.find((item) => item.id === category.key)
        return {
          id: category.key,
          title: category.name,
          href: `/${category.key}`,
          image: staticCategory?.image ?? categories[index % categories.length].image,
          productCount: category.productCount,
        }
      })
    : categories.map((category) => ({ ...category, productCount: undefined }))

  useEffect(() => {
    if (!open) return
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = prev
    }
  }, [open])

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  const cartLabel = 'Cart'
  const wishLabel = 'Wishlist'

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          role="dialog"
          aria-modal="true"
          aria-label="Site navigation"
          className="fixed inset-0 z-[60] flex flex-col bg-canvas/98 backdrop-blur-xl"
          variants={reduced ? undefined : panel}
          initial="hidden"
          animate="show"
          exit="exit"
        >
          <div className="shrink-0 border-b border-outline/40 px-margin-mobile py-3 md:px-margin-desktop">
            <div className="mx-auto flex max-w-container-max items-center gap-3">
              <div className="min-w-0 flex-1">
                <p className="label-caps text-brand">Menu</p>
                <p className="truncate text-xs text-ink-muted">{site.name}</p>
              </div>

              <div className="flex shrink-0 items-center gap-0.5 rounded-full border border-outline/60 bg-surface p-0.5">
                <MenuIconBtn to="/cart" onClick={onClose} label={cartLabel} count={cartCount}>
                  <CartIcon />
                </MenuIconBtn>
                <MenuIconBtn to="/wishlist" onClick={onClose} label={wishLabel} count={wishlistCount}>
                  <HeartIcon />
                </MenuIconBtn>
                {profile ? (
                  <MenuIconBtn
                    label="Clear retailer profile"
                    onClick={() => {
                      clearProfile()
                      onClose()
                    }}
                  >
                    <UserIcon />
                  </MenuIconBtn>
                ) : (
                  <>
                    <MenuIconBtn to="/login" onClick={onClose} label="Retailer account">
                      <UserIcon />
                    </MenuIconBtn>
                    <MenuIconBtn to="/register" onClick={onClose} label="Create retailer profile">
                      <UserPlusIcon />
                    </MenuIconBtn>
                  </>
                )}
                <span className="mx-0.5 h-5 w-px bg-outline/60" aria-hidden />
                <MenuIconBtn
                  href={generalEnquiryUrl()}
                  onClick={onClose}
                  label="WhatsApp"
                  external
                >
                  <WhatsAppIcon />
                </MenuIconBtn>
              </div>

              <button
                type="button"
                onClick={onClose}
                aria-label="Close menu"
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-outline/70 bg-surface text-ink transition-colors hover:border-brand hover:text-brand"
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
                  <path d="M18 6 6 18M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>

          <div className="flex flex-1 flex-col overflow-y-auto overscroll-contain">
            <div className="px-margin-mobile py-6 md:px-margin-desktop md:py-8">
              <div className="mx-auto grid max-w-container-max gap-8 lg:grid-cols-12 lg:gap-10">
                <nav className="lg:col-span-4" aria-label="Pages">
                  <p className="label-caps text-ink-faint">Pages</p>
                  <ul className="mt-3 divide-y divide-outline/50 overflow-hidden rounded-xl border border-outline/50 bg-surface">
                    {PAGE_LINKS.map((link, i) => (
                      <li key={link.to}>
                        <Link
                          to={link.to}
                          onClick={onClose}
                          className="group flex items-center gap-3 px-4 py-3 transition-colors hover:bg-brand/[0.04]"
                        >
                          <span className="label-caps w-5 shrink-0 text-[10px] text-ink-faint">
                            {String(i + 1).padStart(2, '0')}
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="block text-sm font-medium text-ink group-hover:text-brand">
                              {link.label}
                            </span>
                            <span className="mt-0.5 block text-xs text-ink-faint">{link.hint}</span>
                          </span>
                          <span className="text-ink-faint group-hover:text-brand" aria-hidden>
                            →
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </nav>

                <div className="lg:col-span-8">
                  <p className="label-caps text-brand">Collections</p>
                  <p className="mt-1 text-sm text-ink-muted">Wholesale categories · MOQ on every style</p>

                  <motion.div
                    className="mt-4 grid grid-cols-2 gap-2.5 sm:grid-cols-3 sm:gap-3"
                    variants={reduced ? undefined : collectionGrid}
                    initial="hidden"
                    animate="show"
                  >
                    {collectionCategories.map((cat, i) => (
                      <motion.div key={cat.id} variants={reduced ? undefined : collectionCard}>
                        <Link
                          to={cat.href}
                          onClick={onClose}
                          className="group block overflow-hidden rounded-lg bg-surface ring-1 ring-outline/50 transition-[ring-color,transform] duration-300 hover:-translate-y-0.5 hover:ring-brand/40"
                        >
                          <div className="relative aspect-[4/5] overflow-hidden">
                            <img
                              src={cat.image}
                              alt=""
                              className="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-[1.03]"
                              loading="lazy"
                            />
                            <div className="absolute inset-0 bg-linear-to-t from-obsidian/80 via-obsidian/10 to-transparent transition-opacity duration-300 group-hover:from-obsidian/90" />
                            <div className="absolute inset-x-0 bottom-0 p-2.5 transition-transform duration-300 group-hover:translate-y-[-2px]">
                              <span className="label-caps text-[9px] text-inverse-faint">
                                {String(i + 1).padStart(2, '0')}
                              </span>
                              <p className="mt-0.5 line-clamp-2 text-xs font-medium leading-snug text-inverse">
                                {cat.title}
                              </p>
                              {cat.productCount !== undefined && (
                                <p className="mt-1 text-[10px] text-inverse-faint">
                                  {cat.productCount} {cat.productCount === 1 ? 'product' : 'products'}
                                </p>
                              )}
                            </div>
                          </div>
                        </Link>
                      </motion.div>
                    ))}
                  </motion.div>
                </div>
              </div>
            </div>

            <div className="mt-auto shrink-0 border-t border-outline/30 px-margin-mobile py-3 md:px-margin-desktop">
              <p className="mx-auto max-w-container-max text-center text-[11px] text-ink-faint">
                {site.locationNote} · {site.tagline}
              </p>
            </div>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
