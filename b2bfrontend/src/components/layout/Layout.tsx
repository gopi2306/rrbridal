import { Outlet, useLocation } from 'react-router-dom'
import { Header } from './Header'
import { Footer } from './Footer'
import { WhatsAppFab } from '@/components/ui/WhatsAppFab'
import { BackToTopFab } from '@/components/ui/BackToTopFab'
import { ScrollToTop } from './ScrollToTop'
import { useCatalog } from '@/context/CatalogContext'

export function Layout() {
  const { pathname } = useLocation()
  const { sourceErrors } = useCatalog()
  const isHome = pathname === '/'

  return (
    <div className="flex min-h-screen flex-col bg-canvas">
      <ScrollToTop />
      <Header />
      {sourceErrors.length > 0 && (
        <div className="border-b border-amber-300/40 bg-amber-50 px-4 py-2 text-center text-xs text-amber-900">
          Some business catalogues are temporarily unavailable. Available products are still shown.
        </div>
      )}
      <main className={`flex-1 ${isHome ? '' : 'pt-[var(--header-height)]'}`}>
        <Outlet />
      </main>
      <Footer />
      <BackToTopFab />
      <WhatsAppFab />
    </div>
  )
}
