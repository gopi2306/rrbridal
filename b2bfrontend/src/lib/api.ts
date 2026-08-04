import type { RetailerProfile, StorefrontCategory, StorefrontOffer } from '@/types'

const API_URL = (import.meta.env.VITE_B2B_API_URL || '/api').replace(/\/$/, '')

export interface ApiError {
  databaseKey?: string
  databaseLabel?: string
  error: string
}

interface ProductPage {
  data: StorefrontOffer[]
  total: number
  page: number
  limit: number
  totalPages: number
  errors: ApiError[]
}

interface CategoryResponse {
  data: StorefrontCategory[]
  errors: ApiError[]
}

export interface EnquiryResponse {
  requestId: string
  results: {
    databaseKey: string
    databaseLabel: string
    enquiryId: string
    created: boolean
  }[]
  errors: ApiError[]
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    const detail = await response.text().catch(() => '')
    throw new Error(detail || `Request failed (${response.status})`)
  }
  return response.json() as Promise<T>
}

export async function fetchProducts(signal?: AbortSignal): Promise<ProductPage> {
  const first = await request<ProductPage>(
    '/storefront/products?page=1&limit=100&search=&category=&sort=featured',
    { signal },
  )
  if (first.totalPages <= 1) return first

  const remaining = await Promise.all(
    Array.from({ length: first.totalPages - 1 }, (_, index) =>
      request<ProductPage>(
        `/storefront/products?page=${index + 2}&limit=100&search=&category=&sort=featured`,
        { signal },
      ),
    ),
  )
  return {
    ...first,
    data: [first, ...remaining].flatMap((page) => page.data),
    errors: [first, ...remaining].flatMap((page) => page.errors ?? []),
  }
}

export function fetchCategories(signal?: AbortSignal) {
  return request<CategoryResponse>('/storefront/categories', { signal })
}

export function fetchProduct(databaseKey: string, productId: string, signal?: AbortSignal) {
  return request<StorefrontOffer>(
    `/storefront/products/${encodeURIComponent(databaseKey)}/${encodeURIComponent(productId)}`,
    { signal },
  )
}

export function submitEnquiry(input: {
  requestId?: string
  retailer: RetailerProfile
  lines: { databaseKey: string; productId: string; quantity: number }[]
  note?: string
}) {
  return request<EnquiryResponse>('/storefront/enquiries', {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function productPath(product: Pick<StorefrontOffer, 'databaseKey' | 'productId'>) {
  return `/product/${encodeURIComponent(product.databaseKey)}/${encodeURIComponent(product.productId)}`
}
