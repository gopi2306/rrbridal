import { useRef } from "react";
import { Link } from "react-router-dom";
import { motion, useScroll } from "framer-motion";
import { formatPrice, getProductsByFeatured } from "@/lib/data";
import { getCampaignBanner } from "@/lib/images";
import { SafeImage } from "@/components/ui/SafeImage";
import { getProductImage } from "@/lib/images";
import { titleCase } from "@/lib/format";
import { Stagger, StaggerItem } from "@/components/ui/FadeIn";
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion";
import {
  parallaxTransformTemplate,
  useParallaxPx,
} from "@/hooks/useParallaxPx";
import { useCatalog } from "@/context/CatalogContext";
import { productPath } from "@/lib/api";

export function CampaignOverlay() {
  const { products: catalogProducts } = useCatalog();
  const reduced = usePrefersReducedMotion();
  const sectionRef = useRef<HTMLElement>(null);
  const products = getProductsByFeatured("summer", catalogProducts).slice(0, 4);
  const banner = getCampaignBanner();

  const { scrollYProgress } = useScroll({
    target: sectionRef,
    offset: ["start end", "end start"],
  });
  const bannerY = useParallaxPx(scrollYProgress, -36, 36);

  return (
    <section ref={sectionRef} className="relative pb-16 md:pb-20">
      <div className="relative h-[min(56vh,520px)] w-full overflow-hidden sm:h-[min(55vh,520px)] md:h-[min(60vh,560px)]">
        <motion.div
          className="motion-parallax absolute inset-0"
          style={reduced ? undefined : { y: bannerY }}
          transformTemplate={parallaxTransformTemplate}
        >
          <SafeImage
            src={banner.src}
            fallback={banner.fallback}
            alt="Summer wholesale collection"
            className="h-[115%] w-full object-cover"
          />
        </motion.div>
        <div className="absolute inset-0 bg-linear-to-r from-canvas/92 via-canvas/55 to-canvas/15" />
        <div className="absolute inset-x-0 bottom-0 z-20 px-margin-mobile pb-10 pt-20 sm:pb-8 sm:pt-16 md:inset-x-auto md:left-margin-desktop md:top-1/2 md:max-w-sm md:-translate-y-1/2 md:px-0 md:pb-0 md:pt-0">
          <p className="label-caps text-brand">Summer wholesale</p>
          <h2 className="mt-2 text-2xl font-semibold text-ink sm:text-3xl md:text-4xl">
            Summer collection
          </h2>
          <p className="mt-3 max-w-xs text-sm leading-relaxed text-ink-muted">
            Lightweight suits and kurti sets — bulk-ready stock from Surat, MOQ
            from 4 pcs.
          </p>
          <Link
            to="/readymade"
            className="label-caps mt-6 inline-flex bg-brand px-6 py-3 text-white shadow-[0_12px_28px_-10px_rgba(233,30,140,0.45)] transition-colors hover:bg-brand-dark"
          >
            View collection
          </Link>
        </div>
      </div>

      <div className="relative z-10 mt-6 px-margin-mobile sm:mt-8 md:-mt-20 md:px-margin-desktop">
        <Stagger className="mx-auto grid max-w-container-max grid-cols-2 gap-3 sm:gap-4 md:grid-cols-4 md:gap-5">
          {products.map((product) => (
            <StaggerItem key={product.id}>
              <Link to={productPath(product)} className="group block">
                <div className="aspect-[3/4] overflow-hidden rounded-sm border border-outline/40 bg-surface shadow-soft transition-all duration-500 group-hover:-translate-y-1 group-hover:border-brand/40 group-hover:shadow-elevated">
                  <img
                    src={getProductImage(product)}
                    alt={product.name}
                    className="image-hover-zoom h-full w-full object-cover"
                  />
                </div>
                <p className="mt-3 line-clamp-1 text-sm font-medium text-ink transition-colors group-hover:text-brand">
                  {titleCase(product.name)}
                </p>
                <p className="mt-0.5 text-xs tabular-nums text-ink-muted">
                  MOQ {product.moq} · {formatPrice(product.price)}
                </p>
              </Link>
            </StaggerItem>
          ))}
        </Stagger>
      </div>
    </section>
  );
}
