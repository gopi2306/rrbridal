import { lazy, Suspense } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { Layout } from '@/components/layout/Layout'
import { isComingSoonEnabled } from '@/lib/featureFlags'
import { ComingSoonPage } from '@/pages/ComingSoonPage'

const HomePage = lazy(() => import('@/pages/HomePage').then((m) => ({ default: m.HomePage })))
const CategoryPage = lazy(() => import('@/pages/CategoryPage').then((m) => ({ default: m.CategoryPage })))
const ProductPage = lazy(() => import('@/pages/ProductPage').then((m) => ({ default: m.ProductPage })))
const AboutPage = lazy(() => import('@/pages/AboutPage').then((m) => ({ default: m.AboutPage })))
const ContactPage = lazy(() => import('@/pages/ContactPage').then((m) => ({ default: m.ContactPage })))
const CartPage = lazy(() => import('@/pages/CartPage').then((m) => ({ default: m.CartPage })))
const WishlistPage = lazy(() => import('@/pages/WishlistPage').then((m) => ({ default: m.WishlistPage })))
const LoginPage = lazy(() => import('@/pages/LoginPage').then((m) => ({ default: m.LoginPage })))
const RegisterPage = lazy(() => import('@/pages/RegisterPage').then((m) => ({ default: m.RegisterPage })))

function PageLoader() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center">
      <div className="h-6 w-6 animate-spin rounded-full border-2 border-brand/40 border-t-brand" />
    </div>
  )
}

export default function App() {
  if (isComingSoonEnabled) {
    return (
      <BrowserRouter>
        <Routes>
          <Route path="*" element={<ComingSoonPage />} />
        </Routes>
      </BrowserRouter>
    )
  }

  return (
    <BrowserRouter>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route element={<Layout />}>
            <Route index element={<HomePage />} />
            <Route path="about" element={<AboutPage />} />
            <Route path="contact" element={<ContactPage />} />
            <Route path="cart" element={<CartPage />} />
            <Route path="wishlist" element={<WishlistPage />} />
            <Route path="login" element={<LoginPage />} />
            <Route path="register" element={<RegisterPage />} />
            <Route path="product/:databaseKey/:productId" element={<ProductPage />} />
            <Route path=":categoryId/*" element={<CategoryPage />} />
          </Route>
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}
