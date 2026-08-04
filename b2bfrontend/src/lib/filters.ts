import type { Product } from '@/types'

export type SortOption = 'featured' | 'price-asc' | 'price-desc' | 'popular' | 'newest'

export type HighlightFilter = NonNullable<Product['featured']>

export interface ProductFiltersState {
  priceMin: number
  priceMax: number
  moq: number[]
  fabrics: string[]
  bottoms: string[]
  dupattas: string[]
  subcategories: string[]
  highlights: HighlightFilter[]
  sort: SortOption
}

export interface FilterMeta {
  priceMin: number
  priceMax: number
  moqOptions: number[]
  fabricOptions: string[]
  bottomOptions: string[]
  dupattaOptions: string[]
  subcategoryOptions: { value: string; label: string }[]
  highlightOptions: { value: HighlightFilter; label: string }[]
}

const HIGHLIGHT_LABELS: Record<HighlightFilter, string> = {
  summer: 'Summer picks',
  month: 'Product of the month',
  new: 'New arrivals',
}

export function buildFilterMeta(products: Product[], categoryChildren?: { label: string; href: string }[]): FilterMeta {
  const prices = products.map((p) => p.price)
  const subcategoryOptions =
    categoryChildren?.map((child) => ({
      value: child.href.split('/').pop() ?? '',
      label: child.label,
    })) ?? []

  const highlightValues = [...new Set(products.map((p) => p.featured).filter(Boolean))] as HighlightFilter[]

  return {
    priceMin: prices.length ? Math.min(...prices) : 0,
    priceMax: prices.length ? Math.max(...prices) : 0,
    moqOptions: [...new Set(products.map((p) => p.moq))].sort((a, b) => a - b),
    fabricOptions: [...new Set(products.map((p) => p.specifications.fabric).filter(Boolean))].sort(),
    bottomOptions: [...new Set(products.map((p) => p.specifications.bottom).filter(Boolean))].sort(),
    dupattaOptions: [...new Set(products.map((p) => p.specifications.dupatta).filter(Boolean))].sort(),
    subcategoryOptions,
    highlightOptions: highlightValues.map((value) => ({
      value,
      label: HIGHLIGHT_LABELS[value],
    })),
  }
}

export function createDefaultFilters(meta: FilterMeta): ProductFiltersState {
  return {
    priceMin: meta.priceMin,
    priceMax: meta.priceMax,
    moq: [],
    fabrics: [],
    bottoms: [],
    dupattas: [],
    subcategories: [],
    highlights: [],
    sort: 'featured',
  }
}

export function countActiveFilters(state: ProductFiltersState, defaults: ProductFiltersState): number {
  let count = 0
  if (state.priceMin > defaults.priceMin || state.priceMax < defaults.priceMax) count++
  if (state.moq.length) count++
  if (state.fabrics.length) count++
  if (state.bottoms.length) count++
  if (state.dupattas.length) count++
  if (state.subcategories.length) count++
  if (state.highlights.length) count++
  return count
}

function sortProducts(list: Product[], sort: SortOption): Product[] {
  const copy = [...list]
  switch (sort) {
    case 'price-asc':
      return copy.sort((a, b) => a.price - b.price)
    case 'price-desc':
      return copy.sort((a, b) => b.price - a.price)
    case 'popular':
      return copy.sort((a, b) => b.views - a.views)
    case 'newest':
      return copy.reverse()
    case 'featured':
    default:
      return copy.sort((a, b) => {
        const af = a.featured ? 1 : 0
        const bf = b.featured ? 1 : 0
        if (bf !== af) return bf - af
        return b.views - a.views
      })
  }
}

export function applyProductFilters(products: Product[], state: ProductFiltersState): Product[] {
  const filtered = products.filter((product) => {
    if (product.price < state.priceMin || product.price > state.priceMax) return false
    if (state.moq.length && !state.moq.includes(product.moq)) return false
    if (state.fabrics.length && !state.fabrics.includes(product.specifications.fabric)) return false
    if (state.bottoms.length && !state.bottoms.includes(product.specifications.bottom)) return false
    if (state.dupattas.length && !state.dupattas.includes(product.specifications.dupatta)) return false
    if (state.subcategories.length) {
      const sub = product.categoryPath.split('/')[1]
      if (!sub || !state.subcategories.includes(sub)) return false
    }
    if (state.highlights.length && (!product.featured || !state.highlights.includes(product.featured))) {
      return false
    }
    return true
  })

  return sortProducts(filtered, state.sort)
}

export const SORT_LABELS: Record<SortOption, string> = {
  featured: 'Featured',
  'price-asc': 'Price: Low to High',
  'price-desc': 'Price: High to Low',
  popular: 'Most Popular',
  newest: 'Newest',
}
