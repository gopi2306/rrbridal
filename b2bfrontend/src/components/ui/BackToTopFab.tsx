import { useEffect, useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { useLocation } from 'react-router-dom'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

const SHOW_AFTER_PX = 480

export function BackToTopFab() {
  const reduced = usePrefersReducedMotion()
  const { pathname } = useLocation()
  const onProductPage = pathname.startsWith('/product/')
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    const onScroll = () => setVisible(window.scrollY > SHOW_AFTER_PX)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  const scrollUp = () => window.scrollTo({ top: 0, behavior: reduced ? 'auto' : 'smooth' })

  return (
    <AnimatePresence>
      {visible && (
        <motion.button
          type="button"
          onClick={scrollUp}
          aria-label="Back to top"
          title="Back to top"
          initial={reduced ? false : { opacity: 0, scale: 0.8, y: 8 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={reduced ? undefined : { opacity: 0, scale: 0.8, y: 8 }}
          transition={{ duration: 0.2 }}
          className={`fixed right-4 z-50 flex h-11 w-11 cursor-pointer items-center justify-center rounded-full border border-ink/10 bg-surface text-ink shadow-elevated transition-colors hover:border-brand hover:text-brand sm:right-6 ${
            onProductPage ? 'fab-bottom-above-sticky lg:bottom-[max(5.5rem,calc(1.5rem+env(safe-area-inset-bottom,0px)+3.5rem))]' : 'fab-bottom-above'
          }`}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M12 19V5" />
            <path d="m5 12 7-7 7 7" />
          </svg>
        </motion.button>
      )}
    </AnimatePresence>
  )
}
