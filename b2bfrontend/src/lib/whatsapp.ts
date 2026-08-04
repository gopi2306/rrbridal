import site from '@/data/site'

export function buildWhatsAppUrl(message: string): string {
  return `https://wa.me/${site.whatsapp}?text=${encodeURIComponent(message)}`
}

export function generalEnquiryUrl(): string {
  return buildWhatsAppUrl('Hi, I want to inquiry for your catalogue.')
}

export function productEnquiryUrl(productName: string, productUrl: string): string {
  return buildWhatsAppUrl(
    `Hi, I want to inquiry for this product:\n\n*${productName}*\n${productUrl}`,
  )
}

export function getProductShareUrl(path: string): string {
  const productPath = path.startsWith('/') ? path : `/product/${path}`
  if (typeof window !== 'undefined') {
    return `${window.location.origin}${productPath}`
  }
  return productPath
}

export interface CartLineForWhatsApp {
  name: string
  quantity: number
  price: number
  slug: string
}

export function cartEnquiryUrl(lines: CartLineForWhatsApp[]): string {
  if (lines.length === 0) return generalEnquiryUrl()

  const items = lines
    .map((line, i) => {
      const url = getProductShareUrl(line.slug)
      return `${i + 1}. *${line.name}* — Qty ${line.quantity} @ ₹${line.price}\n   ${url}`
    })
    .join('\n\n')

  const total = lines.reduce((sum, l) => sum + l.price * l.quantity, 0)

  return buildWhatsAppUrl(
    `Hi, I would like to enquire about the following wholesale order:\n\n${items}\n\n*Estimated total:* ₹${total.toLocaleString('en-IN')}\n\nPlease share availability and bulk pricing.`,
  )
}
