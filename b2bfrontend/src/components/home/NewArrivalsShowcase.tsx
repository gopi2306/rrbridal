import { Link } from "react-router-dom";
import type { Product } from "@/types";
import { formatPrice, getProductsByFeatured } from "@/lib/data";
import { getProductImage } from "@/lib/images";
import { getProductShareUrl, productEnquiryUrl } from "@/lib/whatsapp";
import { titleCase } from "@/lib/format";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { FadeIn } from "@/components/ui/FadeIn";
import { useCatalog } from "@/context/CatalogContext";
import { productPath } from "@/lib/api";

function ArrivalRow({ product, flip }: { product: Product; flip: boolean }) {
  const image = getProductImage(product);
  const whatsappUrl = productEnquiryUrl(
    product.name,
    getProductShareUrl(productPath(product)),
  );

  return (
    <article
      className={`group grid gap-6 border-t border-outline/40 py-8 first:border-t-0 md:grid-cols-2 md:items-center md:gap-10 md:py-10 lg:gap-14 ${
        flip ? "md:[&>*:first-child]:order-2" : ""
      }`}
    >
      <Link to={productPath(product)} className="block">
        <div className="relative aspect-3/4 overflow-hidden border border-outline/50 bg-surface md:max-w-md">
          <img
            src={image}
            alt={product.name}
            loading="lazy"
            className="image-hover-zoom h-full w-full object-cover"
          />
          <span className="label-caps absolute left-3 top-3 border border-outline/60 bg-canvas/80 px-2 py-1 text-[10px] text-ink backdrop-blur-sm">
            MOQ {product.moq}
          </span>
        </div>
      </Link>

      <div className="flex flex-col justify-center md:px-2">
        <p className="label-caps text-brand">New arrival</p>
        <h3 className="mt-3 text-xl font-semibold leading-snug text-ink transition-colors group-hover:text-brand md:text-2xl">
          {titleCase(product.name)}
        </h3>
        <p className="mt-3 text-lg font-semibold tabular-nums text-ink">
          {formatPrice(product.price)}
        </p>
        <p className="mt-2 max-w-sm text-sm leading-relaxed text-ink-muted">
          Wholesale pricing · minimum order {product.moq} pieces · ready for
          bulk dispatch from Surat.
        </p>
        <div className="mt-6 flex flex-wrap items-center gap-3">
          <Link
            to={productPath(product)}
            className="label-caps inline-flex border border-outline/60 px-5 py-2.5 text-ink transition-colors hover:border-brand hover:text-brand"
          >
            View style
          </Link>
          <a
            href={whatsappUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="label-caps inline-flex bg-brand px-5 py-2.5 text-white transition-colors hover:bg-brand-dark"
          >
            WhatsApp enquiry
          </a>
        </div>
      </div>
    </article>
  );
}

export function NewArrivalsShowcase() {
  const { products: catalogProducts } = useCatalog();
  const products = getProductsByFeatured("new", catalogProducts).slice(0, 4);

  if (products.length === 0) return null;

  return (
    <section className="relative section-pad">
      <div className="relative mx-auto max-w-container-max">
        <SectionHeader
          label="Fresh stock"
          title="New arrivals"
          viewAllHref="/readymade"
        />

        <div className="mt-10 overflow-hidden rounded-2xl border border-outline/60 bg-surface-high/40 shadow-soft backdrop-blur-sm">
          <div className="px-4 sm:px-6 md:px-8 lg:px-10">
            {products.map((product, i) => (
              <FadeIn key={product.id} delay={0.06 * i}>
                <ArrivalRow product={product} flip={i % 2 === 1} />
              </FadeIn>
            ))}

            <FadeIn
              delay={0.3}
              className="border-t border-outline/40 py-8 text-center"
            >
              <Link
                to="/readymade"
                className="label-caps text-ink-muted transition-colors hover:text-brand"
              >
                Browse all new stock →
              </Link>
            </FadeIn>
          </div>
        </div>
      </div>
    </section>
  );
}
