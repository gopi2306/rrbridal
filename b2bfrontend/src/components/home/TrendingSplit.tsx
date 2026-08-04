import { useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  AnimatePresence,
  motion,
  useInView,
  useScroll,
  type MotionValue,
  type Variants,
} from "framer-motion";
import type { Product } from "@/types";
import {
  formatPrice,
  getProductsByCategory,
  getProductsByFeatured,
} from "@/lib/data";
import { getProductImage } from "@/lib/images";
import { titleCase } from "@/lib/format";
import { OptimizedImage } from "@/components/ui/SafeImage";
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion";
import {
  parallaxTransformTemplate,
  useParallaxPx,
} from "@/hooks/useParallaxPx";
import { useCatalog } from "@/context/CatalogContext";
import { productPath } from "@/lib/api";

const FILTERS = [
  { id: "new", label: "New", hint: "Fresh drops", tag: "Just in" },
  { id: "month", label: "Bestsellers", hint: "Top reorders", tag: "Hot" },
  { id: "summer", label: "Summer", hint: "Season picks", tag: "Seasonal" },
  { id: "premium", label: "Premium", hint: "Occasion wear", tag: "Occasion" },
] as const;

type FilterId = (typeof FILTERS)[number]["id"];

const PRODUCT_COUNT = 8;
const GRID_COUNT = 5;
const ENTRANCE_EASE = [0.16, 1, 0.3, 1] as const;

const headerContainer: Variants = {
  hidden: {},
  show: {
    transition: { staggerChildren: 0.11, delayChildren: 0.05 },
  },
};

const fadeUp: Variants = {
  hidden: { opacity: 0, y: 24 },
  show: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.7, ease: ENTRANCE_EASE },
  },
};

const cardReveal: Variants = {
  hidden: { opacity: 0, y: 32, scale: 0.97 },
  show: {
    opacity: 1,
    y: 0,
    scale: 1,
    transition: { duration: 0.65, ease: ENTRANCE_EASE },
  },
};

const featuredRow: Variants = {
  hidden: {},
  show: {
    transition: { staggerChildren: 0.14, delayChildren: 0.08 },
  },
};

const gridRow: Variants = {
  hidden: {},
  show: {
    transition: { staggerChildren: 0.07, delayChildren: 0.12 },
  },
};

const filterRow: Variants = {
  hidden: {},
  show: {
    transition: { staggerChildren: 0.06, delayChildren: 0.04 },
  },
};

const filterPill: Variants = {
  hidden: { opacity: 0, y: 14, scale: 0.96 },
  show: {
    opacity: 1,
    y: 0,
    scale: 1,
    transition: { duration: 0.45, ease: ENTRANCE_EASE },
  },
};

const MARQUEE_ITEMS = [
  "Factory-direct pricing",
  "4-piece MOQ",
  "Surat dispatch",
  "Reorder favourites",
  "Wholesale margins",
  "Trending silhouettes",
] as const;

function loadProducts(id: FilterId, source: Product[]): Product[] {
  if (id === "premium")
    return getProductsByCategory("premium", undefined, source).slice(0, PRODUCT_COUNT);
  return getProductsByFeatured(id, source).slice(0, PRODUCT_COUNT);
}

function viewAllHref(id: FilterId): string {
  if (id === "premium") return "/premium";
  return "/readymade";
}

function ArrowIcon({ className = "" }: { className?: string }) {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden
    >
      <path d="M5 12h14M13 6l6 6-6 6" />
    </svg>
  );
}

function TrendingMarquee({ reduced }: { reduced: boolean }) {
  const items = [...MARQUEE_ITEMS, ...MARQUEE_ITEMS];

  return (
    <motion.div
      className="relative mt-6 overflow-hidden border-y border-inverse/10 py-3.5 md:mt-8"
      aria-hidden
      initial={reduced ? false : { opacity: 0, y: 16 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-40px" }}
      transition={{ duration: 0.55, delay: 0.15, ease: ENTRANCE_EASE }}
    >
      <div className="pointer-events-none absolute inset-y-0 left-0 z-10 w-16 bg-linear-to-r from-obsidian to-transparent md:w-24" />
      <div className="pointer-events-none absolute inset-y-0 right-0 z-10 w-16 bg-linear-to-l from-obsidian to-transparent md:w-24" />
      <div className={`flex w-max gap-10 ${reduced ? "" : "marquee-track"}`}>
        {items.map((item, i) => (
          <span
            key={`${item}-${i}`}
            className="label-caps flex shrink-0 items-center gap-10 text-[10px] text-inverse-faint"
          >
            {item}
            <span className="h-1 w-1 rounded-full bg-brand/70" />
          </span>
        ))}
      </div>
    </motion.div>
  );
}

function FilterBar({
  active,
  onChange,
  reduced,
}: {
  active: FilterId;
  onChange: (id: FilterId) => void;
  reduced: boolean;
}) {
  return (
    <motion.nav
      className="mt-6 flex gap-2 overflow-x-auto hide-scrollbar pb-1 md:mt-8 md:flex-wrap md:overflow-visible"
      aria-label="Trending product filters"
      variants={reduced ? undefined : filterRow}
      initial={reduced ? false : "hidden"}
      whileInView={reduced ? undefined : "show"}
      viewport={{ once: true, margin: "-40px" }}
    >
      {FILTERS.map((f) => {
        const isActive = active === f.id;
        return (
          <motion.button
            key={f.id}
            type="button"
            variants={reduced ? undefined : filterPill}
            onClick={() => onChange(f.id)}
            className={`group relative shrink-0 rounded-full px-5 py-3 text-left transition-colors md:px-6 md:py-3.5 ${
              isActive
                ? "text-inverse"
                : "text-inverse-muted hover:text-inverse"
            }`}
          >
            {isActive && !reduced ? (
              <motion.span
                layoutId="trending-filter-bg"
                className="absolute inset-0 rounded-full border border-brand/40 bg-brand/15 shadow-[0_0_32px_-8px_rgba(233,30,140,0.55)]"
                transition={{ type: "spring", stiffness: 420, damping: 32 }}
              />
            ) : isActive ? (
              <span className="absolute inset-0 rounded-full border border-brand/40 bg-brand/15" />
            ) : null}
            <span className="relative flex items-center gap-3">
              <span className="label-caps">{f.label}</span>
              <span
                className={`hidden text-[10px] md:inline ${
                  isActive ? "text-inverse-muted" : "text-inverse-faint"
                }`}
              >
                {f.hint}
              </span>
            </span>
          </motion.button>
        );
      })}
    </motion.nav>
  );
}

function ProductImage({
  product,
  scrollYProgress,
  depth,
  reduced,
  className = "",
  overlayClassName = "",
  eager = false,
  kenBurns = false,
  compact = false,
}: {
  product: Product;
  scrollYProgress: MotionValue<number>;
  depth: number;
  reduced: boolean;
  className?: string;
  overlayClassName?: string;
  eager?: boolean;
  kenBurns?: boolean;
  compact?: boolean;
}) {
  const spread = Math.round(depth * 28);
  const imageY = useParallaxPx(scrollYProgress, -spread, spread);
  const image = getProductImage(product);

  return (
    <div
      className={`relative isolate size-full overflow-hidden bg-obsidian ${className}`}
    >
      <motion.div
        className={
          compact
            ? "absolute inset-0 size-full"
            : "absolute top-[-8%] left-0 h-[116%] w-full motion-parallax"
        }
        style={reduced || compact ? undefined : { y: imageY }}
        transformTemplate={compact ? undefined : parallaxTransformTemplate}
        initial={{ scale: 1 }}
        animate={kenBurns && !reduced ? { scale: 1.05 } : { scale: 1 }}
        transition={{
          scale: { duration: kenBurns && !reduced ? 14 : 0, ease: "linear" },
        }}
      >
        <div className="size-full [&_img]:size-full [&_img]:object-cover [&_img]:object-center">
          <OptimizedImage
            src={image}
            alt={product.name}
            loading={eager ? "eager" : "lazy"}
            className="size-full object-cover object-center transition-transform duration-500 ease-out group-hover:scale-[1.04]"
          />
        </div>
      </motion.div>
      {overlayClassName ? (
        <div
          className={`pointer-events-none absolute inset-0 ${overlayClassName}`}
        />
      ) : null}
    </div>
  );
}

function FeaturedPortraitCard({
  product,
  filterTag,
  scrollYProgress,
  reduced,
}: {
  product: Product;
  filterTag: string;
  scrollYProgress: MotionValue<number>;
  reduced: boolean;
}) {
  return (
    <motion.article
      className="group relative overflow-hidden rounded-2xl border border-inverse/12 bg-obsidian shadow-[0_24px_60px_-20px_rgba(0,0,0,0.6)]"
      variants={reduced ? undefined : cardReveal}
      whileHover={
        reduced
          ? undefined
          : { y: -6, transition: { duration: 0.35, ease: ENTRANCE_EASE } }
      }
    >
      <div className="relative aspect-3/4 overflow-hidden">
        <ProductImage
          product={product}
          scrollYProgress={scrollYProgress}
          depth={1.1}
          reduced={reduced}
          eager
          kenBurns
          className="absolute inset-0"
          overlayClassName="bg-linear-to-t from-obsidian/80 via-obsidian/15 to-transparent"
        />

        <span className="label-caps absolute left-4 top-4 z-10 rounded-full border border-brand/50 bg-brand/20 px-3 py-1.5 text-[10px] text-brand-soft ">
          {filterTag}
        </span>

        <div className="absolute inset-x-0 bottom-0 z-10 bg-linear-to-t from-obsidian from-25% to-transparent px-5 pb-5 pt-20">
          <h3 className="font-accent text-[clamp(1.25rem,2.5vw,1.75rem)] font-medium italic leading-snug tracking-tight text-inverse">
            {titleCase(product.name)}
          </h3>
          <p className="mt-2 text-xl font-semibold tabular-nums text-inverse">
            {formatPrice(product.price)}
          </p>
          <p className="mt-1 text-xs text-inverse-muted">
            MOQ {product.moq} pieces
          </p>
          <span className="label-caps mt-4 inline-flex items-center gap-2 text-brand transition-[gap] duration-300 group-hover:gap-3">
            View style
            <ArrowIcon className="text-brand" />
          </span>
        </div>
      </div>

      <Link
        to={productPath(product)}
        className="absolute inset-0 z-20 rounded-2xl focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-brand"
        aria-label={`View ${product.name}`}
      />
    </motion.article>
  );
}

function GridCardImage({ product }: { product: Product }) {
  const src = getProductImage(product);

  return (
    <div className="absolute inset-0 ">
      <img
        src={src}
        alt={product.name}
        loading="lazy"
        decoding="async"
        className="block size-full object-cover object-center transition-transform duration-500 ease-out group-hover:scale-[1.04]"
      />
    </div>
  );
}

function GridCard({
  product,
  reduced,
}: {
  product: Product;
  reduced: boolean;
}) {
  return (
    <motion.article
      className="group relative aspect-3/4 overflow-hidden rounded-xl border border-inverse/12 bg-obsidian shadow-[0_16px_48px_-24px_rgba(0,0,0,0.55)]"
      variants={reduced ? undefined : cardReveal}
    >
      <GridCardImage product={product} />

      <span className="label-caps absolute left-3 top-3 z-10 rounded-full border border-inverse/20 bg-obsidian/70 px-2.5 py-1 text-[10px] text-inverse backdrop-blur-sm">
        MOQ {product.moq}
      </span>

      <div className="absolute inset-x-0 bottom-0 z-20 bg-linear-to-t from-obsidian from-0% via-obsidian/85 via-45% to-transparent px-4 pb-3.5 pt-14">
        <h3 className="line-clamp-2 text-sm font-medium leading-snug text-inverse transition-colors group-hover:text-brand-soft md:text-base">
          {titleCase(product.name)}
        </h3>
        <div className="mt-2 flex items-center justify-between gap-2">
          <p className="text-sm font-semibold tabular-nums text-inverse">
            {formatPrice(product.price)}
          </p>
          <span
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-inverse/25 bg-inverse/10 text-inverse transition-[background-color,border-color,transform] duration-300 group-hover:scale-105 group-hover:border-brand/40 group-hover:bg-brand/15"
            aria-hidden
          >
            <ArrowIcon />
          </span>
        </div>
      </div>

      <Link
        to={productPath(product)}
        className="absolute inset-0 z-30 rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-brand focus-visible:ring-offset-2 focus-visible:ring-offset-obsidian"
        aria-label={`View ${product.name}`}
      />
    </motion.article>
  );
}

export function TrendingSplit() {
  const { products: catalogProducts, loading, error } = useCatalog();
  const reduced = usePrefersReducedMotion();
  const sectionRef = useRef<HTMLElement>(null);
  const productsStageRef = useRef<HTMLDivElement>(null);
  const productsInView = useInView(productsStageRef, {
    once: true,
    margin: "-80px",
  });
  const [active, setActive] = useState<FilterId>("new");
  const products = useMemo(() => loadProducts(active, catalogProducts), [active, catalogProducts]);
  const activeFilter = FILTERS.find((f) => f.id === active)!;

  const { scrollYProgress } = useScroll({
    target: sectionRef,
    offset: ["start end", "end start"],
  });
  const orbY = useParallaxPx(scrollYProgress, -40, 60);
  const orbYAlt = useParallaxPx(scrollYProgress, 50, -30);

  const featuredPair = products.slice(0, 2);
  const gridProducts = products.slice(2, 2 + GRID_COUNT);

  if (loading) return <div className="bg-obsidian py-20 text-center text-inverse-muted">Loading live wholesale picks…</div>;
  if (error) return <div className="bg-obsidian py-20 text-center text-red-300">Live catalogue unavailable: {error}</div>;
  if (products.length === 0) return null;

  return (
    <section
      ref={sectionRef}
      className="relative isolate overflow-hidden bg-obsidian px-margin-mobile pt-10 pb-20 text-inverse md:px-margin-desktop md:pt-12 md:pb-24"
    >
      <div
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_90%_60%_at_80%_0%,rgba(233,30,140,0.14),transparent_55%)]"
        aria-hidden
      />
      <div
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_70%_50%_at_10%_100%,rgba(184,134,11,0.08),transparent_50%)]"
        aria-hidden
      />
      <div
        className="hero-grain pointer-events-none absolute inset-0 opacity-[0.035]"
        aria-hidden
      />

      <motion.div
        className="pointer-events-none absolute -right-20 top-1/4 h-72 w-72 rounded-full bg-brand/20 blur-[100px] motion-parallax"
        style={reduced ? undefined : { y: orbY }}
        transformTemplate={parallaxTransformTemplate}
        aria-hidden
      />
      <motion.div
        className="pointer-events-none absolute -left-24 bottom-1/4 h-56 w-56 rounded-full bg-gold/10 blur-[90px] motion-parallax"
        style={reduced ? undefined : { y: orbYAlt }}
        transformTemplate={parallaxTransformTemplate}
        aria-hidden
      />

      <div className="relative z-10 mx-auto max-w-container-max">
        <motion.header
          className="grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)] lg:items-end lg:gap-12"
          variants={reduced ? undefined : headerContainer}
          initial={reduced ? false : "hidden"}
          whileInView={reduced ? undefined : "show"}
          viewport={{ once: true, margin: "-50px" }}
        >
          <motion.div variants={reduced ? undefined : fadeUp}>
            <p className="label-caps text-brand">Wholesale picks</p>
            <h2 className="mt-2 text-[clamp(1.875rem,4.5vw,4rem)] leading-[1.06] tracking-tight">
              <span className="font-accent italic">Trending</span>
            </h2>
          </motion.div>

          <motion.div
            variants={reduced ? undefined : fadeUp}
            className="flex flex-col gap-5 border-t border-inverse/10 pt-8 lg:border-t-0 lg:border-l lg:pl-12 lg:pt-0"
          >
            <p className="font-accent text-xl leading-relaxed text-inverse-muted italic md:text-2xl md:leading-normal">
              Retailer favourites that move fast — curated rails by drop,
              season, and occasion. Tap a lane to refresh the edit.
            </p>
            <Link
              to={viewAllHref(active)}
              className="label-caps group inline-flex w-fit items-center gap-3 border-b border-brand/40 pb-1 text-brand transition-[gap,color] duration-300 hover:gap-4 hover:text-inverse"
            >
              View all styles
              <span className="transition-transform group-hover:translate-x-1">
                →
              </span>
            </Link>
          </motion.div>
        </motion.header>

        <TrendingMarquee reduced={reduced} />

        <FilterBar active={active} onChange={setActive} reduced={reduced} />

        <motion.div ref={productsStageRef} className="mt-8 md:mt-10">
          <motion.div
            className="mb-6 flex flex-wrap items-end justify-between gap-4 border-b border-inverse/10 pb-5"
            initial={reduced ? false : { opacity: 0, y: 16 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true, margin: "-40px" }}
            transition={{ duration: 0.5, delay: 0.1, ease: ENTRANCE_EASE }}
          >
            <div>
              <p className="label-caps text-inverse-faint">Current edit</p>
              <p className="mt-1.5 text-lg font-medium text-inverse md:text-xl">
                {activeFilter.label}
              </p>
            </div>
            <span className="label-caps rounded-full border border-inverse/15 bg-inverse/5 px-3.5 py-1.5 text-[10px] text-inverse-muted">
              {activeFilter.tag}
            </span>
          </motion.div>

          <AnimatePresence mode="wait">
            <motion.div
              key={active}
              className="flex flex-col gap-8 sm:gap-9 lg:gap-10"
              initial={reduced ? false : { opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={reduced ? undefined : { opacity: 0 }}
              transition={{ duration: 0.3 }}
            >
              <motion.div
                className="grid gap-4 sm:grid-cols-2 sm:gap-5 lg:gap-6"
                variants={reduced ? undefined : featuredRow}
                initial={reduced ? false : "hidden"}
                animate={reduced || productsInView ? "show" : "hidden"}
              >
                {featuredPair.map((product) => (
                  <FeaturedPortraitCard
                    key={product.id}
                    product={product}
                    filterTag={activeFilter.tag}
                    scrollYProgress={scrollYProgress}
                    reduced={reduced}
                  />
                ))}
              </motion.div>

              <motion.div
                className="grid grid-cols-2 gap-4 sm:grid-cols-3 sm:gap-5 lg:grid-cols-5 lg:gap-6"
                variants={reduced ? undefined : gridRow}
                initial={reduced ? false : "hidden"}
                animate={reduced || productsInView ? "show" : "hidden"}
              >
                {gridProducts.map((product) => (
                  <GridCard
                    key={product.id}
                    product={product}
                    reduced={reduced}
                  />
                ))}
              </motion.div>
            </motion.div>
          </AnimatePresence>
        </motion.div>

        <motion.footer
          className="mt-14 flex flex-col items-center gap-5 border-t border-inverse/10 pt-12 md:mt-20 md:flex-row md:justify-between md:pt-14"
          initial={reduced ? false : { opacity: 0 }}
          whileInView={{ opacity: 1 }}
          viewport={{ once: true }}
          transition={{ duration: 0.6, ease: ENTRANCE_EASE }}
        >
          <p className="max-w-lg text-center text-sm leading-relaxed text-inverse-muted md:text-left md:text-base">
            Stock what your customers ask for — tap any style for full details,
            MOQ, and enquiry options.
          </p>
          <Link
            to={viewAllHref(active)}
            className="label-caps inline-flex items-center gap-3 rounded-full border border-inverse/20 bg-inverse/5 px-6 py-3 text-inverse transition-[gap,background-color,border-color] duration-300 hover:gap-4 hover:border-brand/40 hover:bg-brand/10"
          >
            Browse {activeFilter.label.toLowerCase()}
            <span aria-hidden>→</span>
          </Link>
        </motion.footer>
      </div>
    </section>
  );
}
