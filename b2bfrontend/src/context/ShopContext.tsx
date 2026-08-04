import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import type { Product } from '@/types'
import type { RetailerProfile } from '@/types'
import { useCatalog } from '@/context/CatalogContext'

export interface CartLine {
  id: string
  quantity: number
}

interface ShopContextValue {
  profile: RetailerProfile | null
  cart: CartLine[]
  wishlist: string[]
  cartCount: number
  wishlistCount: number
  saveProfile: (profile: RetailerProfile) => boolean
  clearProfile: () => void
  addToCart: (product: Product, quantity?: number) => void
  removeFromCart: (id: string) => void
  updateCartQty: (id: string, quantity: number) => void
  removeCartLines: (ids: string[]) => void
  clearCart: () => void
  toggleWishlist: (id: string) => void
  isInWishlist: (id: string) => boolean
  getCartProducts: () => { product: Product; quantity: number }[]
}

const ShopContext = createContext<ShopContextValue | null>(null)

const CART_KEY = 'bilal_cart'
const WISH_KEY = 'bilal_wishlist'
const PROFILE_KEY = 'bilal_retailer_profile'

function loadJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : fallback
  } catch {
    return fallback
  }
}

export function ShopProvider({ children }: { children: ReactNode }) {
  const { getProduct } = useCatalog()
  const [cart, setCart] = useState<CartLine[]>(() =>
    loadJson<unknown[]>(CART_KEY, []).filter(
      (line): line is CartLine =>
        typeof line === 'object' &&
        line !== null &&
        typeof (line as CartLine).id === 'string' &&
        typeof (line as CartLine).quantity === 'number',
    ),
  )
  const [wishlist, setWishlist] = useState<string[]>(() =>
    loadJson<unknown[]>(WISH_KEY, []).filter((id): id is string => typeof id === 'string' && id.includes(':')),
  )
  const [profile, setProfile] = useState<RetailerProfile | null>(() => loadJson(PROFILE_KEY, null))

  useEffect(() => {
    localStorage.setItem(CART_KEY, JSON.stringify(cart))
  }, [cart])

  useEffect(() => {
    localStorage.setItem(WISH_KEY, JSON.stringify(wishlist))
  }, [wishlist])

  useEffect(() => {
    if (profile) localStorage.setItem(PROFILE_KEY, JSON.stringify(profile))
    else localStorage.removeItem(PROFILE_KEY)
  }, [profile])

  const addToCart = useCallback((product: Product, quantity = product.moq) => {
    setCart((prev) => {
      const existing = prev.find((l) => l.id === product.id)
      if (existing) {
        return prev.map((l) =>
          l.id === product.id ? { ...l, quantity: l.quantity + quantity } : l,
        )
      }
      return [...prev, { id: product.id, quantity: Math.max(quantity, product.moq) }]
    })
  }, [])

  const removeFromCart = useCallback((id: string) => {
    setCart((prev) => prev.filter((l) => l.id !== id))
  }, [])

  const updateCartQty = useCallback((id: string, quantity: number) => {
    setCart((prev) =>
      prev.map((l) => (l.id === id ? { ...l, quantity: Math.max(1, quantity) } : l)),
    )
  }, [])

  const removeCartLines = useCallback((ids: string[]) => {
    const removed = new Set(ids)
    setCart((prev) => prev.filter((line) => !removed.has(line.id)))
  }, [])

  const clearCart = useCallback(() => setCart([]), [])

  const toggleWishlist = useCallback((id: string) => {
    setWishlist((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id],
    )
  }, [])

  const isInWishlist = useCallback((id: string) => wishlist.includes(id), [wishlist])

  const saveProfile = useCallback((next: RetailerProfile) => {
    if (!next.businessName.trim() || !next.contactName.trim() || !next.email.trim() || !next.phone.trim()) return false
    setProfile(next)
    return true
  }, [])

  const clearProfile = useCallback(() => setProfile(null), [])

  const getCartProducts = useCallback(() => {
    return cart
      .map((line) => {
        const product = getProduct(line.id)
        return product ? { product, quantity: line.quantity } : null
      })
      .filter((x): x is { product: Product; quantity: number } => x !== null)
  }, [cart, getProduct])

  const value = useMemo<ShopContextValue>(
    () => ({
      profile,
      cart,
      wishlist,
      cartCount: cart.reduce((n, l) => n + l.quantity, 0),
      wishlistCount: wishlist.length,
      saveProfile,
      clearProfile,
      addToCart,
      removeFromCart,
      updateCartQty,
      removeCartLines,
      clearCart,
      toggleWishlist,
      isInWishlist,
      getCartProducts,
    }),
    [
      profile,
      cart,
      wishlist,
      saveProfile,
      clearProfile,
      addToCart,
      removeFromCart,
      updateCartQty,
      removeCartLines,
      clearCart,
      toggleWishlist,
      isInWishlist,
      getCartProducts,
    ],
  )

  return <ShopContext.Provider value={value}>{children}</ShopContext.Provider>
}

export function useShop() {
  const ctx = useContext(ShopContext)
  if (!ctx) throw new Error('useShop must be used within ShopProvider')
  return ctx
}
