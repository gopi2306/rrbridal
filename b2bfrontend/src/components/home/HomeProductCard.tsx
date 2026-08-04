import { Link } from 'react-router-dom'
import type { Product } from '@/types'
import { formatPrice } from '@/lib/data'
import { getProductImage } from '@/lib/images'
import { getProductShareUrl, productEnquiryUrl } from '@/lib/whatsapp'
import { productPath } from '@/lib/api'
import { titleCase } from '@/lib/format'

interface HomeProductCardProps {
  product: Product
  size?: 'sm' | 'md' | 'lg'
  showEnquiry?: boolean
}

const sizeClass = {
  sm: 'text-sm',
  md: 'text-base',
  lg: 'text-lg',
} as const

export function HomeProductCard({ product, size = 'md', showEnquiry = true }: HomeProductCardProps) {
  const image = getProductImage(product)
  const whatsappUrl = productEnquiryUrl(product.name, getProductShareUrl(productPath(product)))

  return (
    <article className="group">
      <Link to={productPath(product)} className="block">
        <div className="relative aspect-[3/4] overflow-hidden bg-surface">
          <img
            src={image}
            alt={product.name}
            loading="lazy"
            className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-[1.03]"
          />
          <span className="label-caps absolute left-2 top-2 bg-canvas/80 px-2 py-1 text-[10px] text-ink backdrop-blur-sm">
            MOQ {product.moq}
          </span>
        </div>
        <div className="mt-3 space-y-1">
          <h3
            className={`line-clamp-2 font-medium leading-snug text-ink transition-colors group-hover:text-brand ${sizeClass[size]}`}
          >
            {titleCase(product.name)}
          </h3>
          <p className="text-sm font-semibold tabular-nums text-ink">{formatPrice(product.price)}</p>
        </div>
      </Link>
      {showEnquiry && (
        <a
          href={whatsappUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="label-caps mt-2 inline-block text-[10px] text-ink-faint transition-colors hover:text-brand"
        >
          WhatsApp enquiry
        </a>
      )}
    </article>
  )
}
