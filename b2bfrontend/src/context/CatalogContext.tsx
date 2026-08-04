import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { fetchCategories, fetchProducts, type ApiError } from '@/lib/api'
import type { Product, StorefrontCategory, StorefrontOffer } from '@/types'

interface CatalogContextValue {
  products: Product[]
  categories: StorefrontCategory[]
  loading: boolean
  error: string | null
  sourceErrors: ApiError[]
  retry: () => void
  getProduct: (id: string) => Product | undefined
}

const CatalogContext = createContext<CatalogContextValue | null>(null)

function normalizeProduct(product: StorefrontOffer, index: number): Product {
  const subKey =
    typeof product.subCategory === 'string'
      ? product.subCategory
      : product.subCategory?.key || product.subCategory?.name
  return {
    ...product,
    id: product.id || `${product.databaseKey}:${product.productId}`,
    images: product.images ?? [],
    specifications: product.specifications ?? {},
    views: 0,
    categoryPath: [product.category.key, subKey].filter(Boolean).join('/'),
    featured: index < 8 ? 'new' : index < 16 ? 'month' : 'summer',
  }
}

export function CatalogProvider({ children }: { children: ReactNode }) {
  const [products, setProducts] = useState<Product[]>([])
  const [categories, setCategories] = useState<StorefrontCategory[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [sourceErrors, setSourceErrors] = useState<ApiError[]>([])
  const [attempt, setAttempt] = useState(0)

  const retry = useCallback(() => setAttempt((value) => value + 1), [])

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)

    Promise.all([fetchProducts(controller.signal), fetchCategories(controller.signal)])
      .then(([productResponse, categoryResponse]) => {
        setProducts(productResponse.data.map(normalizeProduct))
        setCategories(categoryResponse.data)
        setSourceErrors([...(productResponse.errors ?? []), ...(categoryResponse.errors ?? [])])
      })
      .catch((reason: unknown) => {
        if (controller.signal.aborted) return
        setError(reason instanceof Error ? reason.message : 'Unable to load the live catalogue.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [attempt])

  const getProduct = useCallback(
    (id: string) => products.find((product) => product.id === id),
    [products],
  )

  const value = useMemo(
    () => ({ products, categories, loading, error, sourceErrors, retry, getProduct }),
    [products, categories, loading, error, sourceErrors, retry, getProduct],
  )

  return <CatalogContext.Provider value={value}>{children}</CatalogContext.Provider>
}

export function useCatalog() {
  const value = useContext(CatalogContext)
  if (!value) throw new Error('useCatalog must be used within CatalogProvider')
  return value
}
