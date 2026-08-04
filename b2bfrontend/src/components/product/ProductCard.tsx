import { Link } from "react-router-dom";
import { motion } from "framer-motion";
import type { Product } from "@/types";
import { formatPrice } from "@/lib/data";
import { getProductImage } from "@/lib/images";
import { getProductShareUrl, productEnquiryUrl } from "@/lib/whatsapp";
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion";
import { useShop } from "@/context/ShopContext";
import { titleCase } from "@/lib/format";
import { Tooltip } from "@/components/ui/Tooltip";
import { productPath } from "@/lib/api";

interface ProductCardProps {
  product: Product;
}

export function ProductCard({ product }: ProductCardProps) {
  const reduced = usePrefersReducedMotion();
  const { addToCart, toggleWishlist, isInWishlist } = useShop();
  const image = getProductImage(product);
  const whatsappUrl = productEnquiryUrl(
    product.name,
    getProductShareUrl(productPath(product)),
  );
  const wished = isInWishlist(product.id);
  const available = product.stock.available && product.stock.total >= product.moq;

  return (
    <motion.article
      className="group relative"
      whileHover={reduced ? undefined : { y: -6 }}
      transition={{ duration: 0.35, ease: [0.16, 1, 0.3, 1] }}
    >
      <div className="relative aspect-[3/4] bg-surface">
        <div className="absolute inset-0 overflow-hidden">
          <Link
            to={productPath(product)}
            className="block h-full cursor-pointer"
          >
            <img
              src={image}
              alt={product.name}
              loading="lazy"
              className="image-hover-zoom h-full w-full object-cover"
            />
          </Link>
          <span className="label-caps absolute left-2 top-2 bg-canvas/80 px-2 py-1 text-[10px] text-ink backdrop-blur-sm">
            MOQ {product.moq}
          </span>
          <a
            href={whatsappUrl}
            target="_blank"
            rel="noopener noreferrer"
            onClick={(e) => e.stopPropagation()}
            className="label-caps absolute bottom-2 left-2 right-2 bg-[#25D366] py-2 text-center text-[10px] text-white opacity-100 transition-opacity lg:opacity-0 lg:group-hover:opacity-100"
          >
            WhatsApp enquiry
          </a>
        </div>

        <div className="absolute right-2 top-2 z-10 flex flex-col gap-1.5">
          <Tooltip
            label={wished ? "Remove from wishlist" : "Add to wishlist"}
            side="bottom"
          >
            <button
              type="button"
              aria-label={wished ? "Remove from wishlist" : "Add to wishlist"}
              onClick={() => toggleWishlist(product.id)}
              className={`flex h-8 w-8 items-center justify-center border backdrop-blur-sm transition-colors ${
                wished
                  ? "border-brand bg-brand text-white"
                  : "border-outline bg-canvas/80 text-ink hover:text-brand"
              }`}
            >
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill={wished ? "currentColor" : "none"}
                stroke="currentColor"
                strokeWidth="2"
                aria-hidden
              >
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
              </svg>
            </button>
          </Tooltip>
          <Tooltip label={available ? "Add to cart" : "Out of stock"} side="bottom">
            <button
              type="button"
              aria-label="Add to cart"
              onClick={() => addToCart(product)}
              disabled={!available}
              className="flex h-8 w-8 items-center justify-center border border-outline bg-canvas/80 text-ink backdrop-blur-sm hover:text-brand disabled:cursor-not-allowed disabled:opacity-40"
            >
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                aria-hidden
              >
                <path d="M12 5v14" />
                <path d="M5 12h14" />
              </svg>
            </button>
          </Tooltip>
        </div>
      </div>

      <Link to={productPath(product)} className="mt-3 block space-y-1">
        <h3 className="font-medium leading-snug text-ink transition-colors group-hover:text-brand">
          {titleCase(product.name)}
        </h3>
        <p className="text-sm font-semibold tabular-nums text-ink">
          {formatPrice(product.price)}
        </p>
        <p className="text-xs text-ink-muted">
          {product.databaseLabel} · {available ? `${product.stock.total} available` : 'Out of stock'}
        </p>
      </Link>
    </motion.article>
  );
}
