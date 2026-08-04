import { categories } from '@/data/categories'
import type { Category, Product } from '@/types'

const products: Product[] = []
export { categories, products }

export function getProductBySlug(slug: string): Product | undefined {
  return products.find((p) => p.slug === slug)
}

export function getProductsByFeatured(featured: Product['featured'], source = products): Product[] {
  return source.filter((p) => p.featured === featured)
}

export function getProductsByCategory(categoryId: string, subPath?: string, source = products): Product[] {
  const prefix = subPath ? `${categoryId}/${subPath}` : categoryId
  return source.filter((p) =>
    subPath ? p.categoryPath === prefix : p.categoryPath.startsWith(categoryId),
  )
}

export function getCategoryById(id: string): Category | undefined {
  return categories.find((c) => c.id === id)
}

export function formatPrice(inr: number): string {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 0,
  }).format(inr)
}

function isPremiumLineProduct(product: Product): boolean {
  const path = product.categoryPath
  return (
    path.startsWith('premium/') ||
    path.includes('premium-party') ||
    path.startsWith('partywear/')
  )
}

/** Premium, partywear, and occasion pieces for the homepage premium line section */
export function getPremiumLineProducts(limit = 6, source = products): Product[] {
  const primary = source.filter(isPremiumLineProduct)
  const primaryIds = new Set(primary.map((p) => p.id))

  const secondary = source
    .filter((p) => !primaryIds.has(p.id) && p.moq <= 3 && p.price >= 2295)
    .sort((a, b) => b.price - a.price)

  return [...primary, ...secondary]
    .sort((a, b) => b.price - a.price)
    .filter((p, i, arr) => arr.findIndex((x) => x.id === p.id) === i)
    .slice(0, limit)
}

export function getRelatedProducts(product: Product, limit = 4, source = products): Product[] {
  const categoryId = product.categoryPath.split('/')[0]
  return source
    .filter((p) => p.id !== product.id && p.categoryPath.startsWith(categoryId))
    .slice(0, limit)
}

export function getFeaturedCategories(limit = 4): Category[] {
  const featuredIds = ['readymade', 'partywear', 'pakistani-suits', 'premium']
  return featuredIds
    .map((id) => categories.find((c) => c.id === id))
    .filter((c): c is Category => Boolean(c))
    .slice(0, limit)
}

/** Six categories for the homepage collage grid */
export function getHomeCollageCategories(): Category[] {
  const ids = [
    'readymade',
    'salwar-kameez',
    'partywear',
    'pakistani-suits',
    'premium',
    'dress-material',
  ]
  return ids
    .map((id) => categories.find((c) => c.id === id))
    .filter((c): c is Category => Boolean(c))
}
