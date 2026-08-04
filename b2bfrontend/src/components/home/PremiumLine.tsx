import { useRef } from "react";
import { Link } from "react-router-dom";
import {
  motion,
  useScroll,
  type MotionValue,
  type Variants,
} from "framer-motion";
import type { Product } from "@/types";
import { formatPrice, getPremiumLineProducts } from "@/lib/data";
import { getBridalBanner, getProductImage } from "@/lib/images";
import { getProductShareUrl, productEnquiryUrl } from "@/lib/whatsapp";
import { useCatalog } from "@/context/CatalogContext";
import { productPath } from "@/lib/api";
import { titleCase } from "@/lib/format";
import { SafeImage } from "@/components/ui/SafeImage";
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion";
import {
  parallaxTransformTemplate,
  useParallaxPx,
} from "@/hooks/useParallaxPx";

const ENTRANCE_EASE = [0.16, 1, 0.3, 1] as const;

const MARQUEE_ITEMS = [
  "Heavy zardozi",
  "Designer bridal",
  "Silk & crepe blends",
  "MOQ from 3 pcs",
  "Peak-season margins",
  "Hand-finished detail",
] as const;

const CRAFT_CHIPS = [
  "Heavy handwork",
  "Bridal-ready",
  "High-ticket retail",
  "Factory-direct",
] as const;

const headerContainer: Variants = {
  hidden: {},
  show: { transition: { staggerChildren: 0.12, delayChildren: 0.04 } },
};

const fadeUp: Variants = {
  hidden: { opacity: 0, y: 28 },
  show: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.75, ease: ENTRANCE_EASE },
  },
};

const cardReveal: Variants = {
  hidden: { opacity: 0, y: 36, scale: 0.97 },
  show: {
    opacity: 1,
    y: 0,
    scale: 1,
    transition: { duration: 0.7, ease: ENTRANCE_EASE },
  },
};

const bentoRow: Variants = {
  hidden: {},
  show: { transition: { staggerChildren: 0.1, delayChildren: 0.08 } },
};

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

function CraftMarquee({ reduced }: { reduced: boolean }) {
  const items = [...MARQUEE_ITEMS, ...MARQUEE_ITEMS];

  return (
    <motion.div
      className="relative mt-8 overflow-hidden border-y border-gold/15 py-3.5 md:mt-10"
      aria-hidden
      initial={reduced ? false : { opacity: 0, y: 16 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-40px" }}
      transition={{ duration: 0.55, delay: 0.12, ease: ENTRANCE_EASE }}
    >
      <div className="pointer-events-none absolute inset-y-0 left-0 z-10 w-16 bg-linear-to-r from-obsidian to-transparent md:w-24" />
      <div className="pointer-events-none absolute inset-y-0 right-0 z-10 w-16 bg-linear-to-l from-obsidian to-transparent md:w-24" />
      <div className={`flex w-max gap-10 ${reduced ? "" : "marquee-track"}`}>
        {items.map((item, i) => (
          <span
            key={`${item}-${i}`}
            className="label-caps flex shrink-0 items-center gap-10 text-[10px] text-gold/55"
          >
            {item}
            <span className="h-1 w-1 rounded-full bg-gold/50" />
          </span>
        ))}
      </div>
    </motion.div>
  );
}

function ParallaxPhoto({
  src,
  alt,
  fallback,
  scrollYProgress,
  depth,
  reduced,
  kenBurns = false,
  eager = false,
  fill = false,
  className = "",
  overlayClassName = "",
}: {
  src: string;
  alt: string;
  fallback?: string;
  scrollYProgress: MotionValue<number>;
  depth: number;
  reduced: boolean;
  kenBurns?: boolean;
  eager?: boolean;
  /** Fill a positioned parent (e.g. aspect-ratio card). */
  fill?: boolean;
  className?: string;
  overlayClassName?: string;
}) {
  const spread = Math.round(depth * 30);
  const imageY = useParallaxPx(scrollYProgress, -spread, spread);

  const image = fallback ? (
    <SafeImage
      src={src}
      fallback={fallback}
      alt={alt}
      loading={eager ? "eager" : "lazy"}
      decoding="async"
      className="block size-full object-cover object-center transition-transform duration-500 ease-out group-hover:scale-[1.04]"
    />
  ) : (
    <img
      src={src}
      alt={alt}
      loading={eager ? "eager" : "lazy"}
      decoding="async"
      className="block size-full object-cover object-center transition-transform duration-500 ease-out group-hover:scale-[1.04]"
    />
  );

  if (fill) {
    return (
      <>
        <motion.div
          className="absolute top-[-10%] left-0 h-[120%] w-full overflow-hidden motion-parallax"
          style={reduced ? undefined : { y: imageY }}
          transformTemplate={parallaxTransformTemplate}
          initial={{ scale: 1 }}
          animate={kenBurns && !reduced ? { scale: 1.05 } : { scale: 1 }}
          transition={{
            scale: { duration: kenBurns && !reduced ? 16 : 0, ease: "linear" },
          }}
        >
          {image}
        </motion.div>
        {overlayClassName ? (
          <div
            className={`pointer-events-none absolute inset-0 ${overlayClassName}`}
          />
        ) : null}
      </>
    );
  }

  return (
    <div className={`relative overflow-hidden ${className}`}>
      <motion.div
        className="absolute top-[-10%] left-0 h-[120%] w-full motion-parallax"
        style={reduced ? undefined : { y: imageY }}
        transformTemplate={parallaxTransformTemplate}
        initial={{ scale: 1 }}
        animate={kenBurns && !reduced ? { scale: 1.07 } : { scale: 1 }}
        transition={{
          scale: { duration: kenBurns && !reduced ? 16 : 0, ease: "linear" },
        }}
      >
        {image}
      </motion.div>
      {overlayClassName ? (
        <div
          className={`pointer-events-none absolute inset-0 ${overlayClassName}`}
        />
      ) : null}
    </div>
  );
}

function CinemaPortal({
  banner,
  scrollYProgress,
  reduced,
}: {
  banner: { src: string; fallback: string };
  scrollYProgress: MotionValue<number>;
  reduced: boolean;
}) {
  return (
    <motion.article
      className="group relative mt-12 overflow-hidden rounded-2xl border border-gold/20 shadow-[0_32px_80px_-24px_rgba(0,0,0,0.65)] md:mt-16 lg:mt-20"
      initial={reduced ? false : { opacity: 0, y: 44 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-80px" }}
      transition={{ duration: 0.85, ease: ENTRANCE_EASE }}
    >
      <div className="grid lg:min-h-[min(78vh,680px)] lg:grid-cols-[minmax(0,42%)_minmax(0,58%)]">
        <div className="relative z-10 flex flex-col justify-center gap-8 bg-obsidian px-8 py-12 sm:px-10 sm:py-14 md:px-12 lg:gap-10 lg:px-14 lg:py-20">
          <span className="label-caps inline-flex w-fit rounded-full border border-gold/30 bg-gold/10 px-3.5 py-1.5 text-[10px] text-gold backdrop-blur-sm">
            The atelier edit
          </span>

          <span className="h-px w-16 bg-gold/40" aria-hidden />

          <div className="space-y-5 md:space-y-6">
            <h3 className="font-accent text-[clamp(2rem,5vw,3.5rem)] font-medium italic leading-[1.05] tracking-tight text-inverse">
              Occasion wear, built for boutique margins
            </h3>
            <p className="max-w-md text-base leading-relaxed text-inverse-muted md:text-lg">
              Hand-embroidered suits, designer bridal sets, and premium party
              pieces your buyers reserve for weddings and festivals — graded for
              high-ticket retail, MOQ from 3 pieces.
            </p>
          </div>

          <div className="flex flex-wrap gap-2">
            {CRAFT_CHIPS.map((chip) => (
              <span
                key={chip}
                className="label-caps rounded-full border border-inverse/15 bg-inverse/5 px-3 py-1.5 text-[10px] text-inverse-muted"
              >
                {chip}
              </span>
            ))}
          </div>

          <span className="label-caps inline-flex items-center gap-3 text-gold transition-[gap] duration-300 group-hover:gap-4">
            Enter the collection
            <span aria-hidden>→</span>
          </span>
        </div>

        <ParallaxPhoto
          src={banner.src}
          fallback={banner.fallback}
          alt="Premium and bridal wholesale collection"
          scrollYProgress={scrollYProgress}
          depth={1.5}
          reduced={reduced}
          kenBurns
          eager
          className="relative min-h-[300px] sm:min-h-[380px] lg:min-h-full"
          overlayClassName="bg-linear-to-t from-obsidian/55 via-obsidian/10 to-transparent lg:bg-linear-to-r lg:from-obsidian/30 lg:via-transparent lg:to-transparent"
        />
      </div>

      <Link
        to="/premium"
        className="absolute inset-0 z-20 rounded-2xl focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-gold"
        aria-label="Browse premium collection"
      />
    </motion.article>
  );
}

function SignaturePiece({
  product,
  scrollYProgress,
  reduced,
}: {
  product: Product;
  scrollYProgress: MotionValue<number>;
  reduced: boolean;
}) {
  const image = getProductImage(product);
  const whatsappUrl = productEnquiryUrl(
    product.name,
    getProductShareUrl(productPath(product)),
  );

  return (
    <motion.article
      className="group relative mt-14 overflow-hidden rounded-2xl border border-gold/15 bg-obsidian/80 md:mt-20"
      variants={reduced ? undefined : cardReveal}
      initial={reduced ? false : "hidden"}
      whileInView={reduced ? undefined : "show"}
      viewport={{ once: true, margin: "-60px" }}
    >
      <div className="grid md:grid-cols-2 md:min-h-[420px] lg:min-h-[480px]">
        <ParallaxPhoto
          src={image}
          alt={product.name}
          scrollYProgress={scrollYProgress}
          depth={1.2}
          reduced={reduced}
          kenBurns
          className="relative min-h-[320px] md:min-h-full"
          overlayClassName="bg-linear-to-t from-obsidian/40 via-transparent to-transparent md:bg-linear-to-r md:from-transparent md:via-transparent md:to-obsidian/20"
        />

        <div className="relative flex flex-col justify-center border-t border-gold/10 px-8 py-10 sm:px-10 md:border-t-0 md:border-l md:px-12 md:py-14 lg:px-16">
          <p className="label-caps text-gold">Signature piece</p>
          <h3 className="mt-4 font-accent text-[clamp(1.5rem,3vw,2.25rem)] font-medium italic leading-snug tracking-tight text-inverse">
            {titleCase(product.name)}
          </h3>
          <p className="mt-4 text-2xl font-semibold tabular-nums text-inverse md:text-3xl">
            {formatPrice(product.price)}
          </p>
          <p className="mt-3 max-w-sm text-sm leading-relaxed text-inverse-muted md:text-base">
            Wholesale pricing for occasion-ready stock — minimum order{" "}
            {product.moq} pieces, handwork-rich finish, ready for peak-season
            windows.
          </p>

          <div className="mt-8 flex flex-wrap items-center gap-3">
            <Link
              to={productPath(product)}
              className="label-caps inline-flex items-center gap-2 border border-gold/35 bg-gold/10 px-5 py-2.5 text-gold transition-[gap,background-color] duration-300 hover:gap-3 hover:bg-gold/15"
            >
              View style
              <ArrowIcon />
            </Link>
            <a
              href={whatsappUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="label-caps text-inverse-faint transition-colors hover:text-gold"
            >
              WhatsApp enquiry
            </a>
          </div>
        </div>
      </div>
    </motion.article>
  );
}

function BentoCard({
  product,
  scrollYProgress,
  depth,
  reduced,
  tall = false,
}: {
  product: Product;
  scrollYProgress: MotionValue<number>;
  depth: number;
  reduced: boolean;
  tall?: boolean;
}) {
  const image = getProductImage(product);

  return (
    <motion.article
      className="group relative isolate h-full min-h-[300px] overflow-hidden rounded-xl border border-inverse/12 bg-obsidian shadow-[0_16px_48px_-24px_rgba(0,0,0,0.55)] transition-[border-color,box-shadow] duration-300 hover:border-inverse/25 hover:shadow-[0_20px_52px_-18px_rgba(0,0,0,0.65)]"
      variants={reduced ? undefined : cardReveal}
    >
      <ParallaxPhoto
        src={image}
        alt={product.name}
        scrollYProgress={scrollYProgress}
        depth={depth}
        reduced={reduced}
        fill
        overlayClassName="bg-linear-to-t from-obsidian/90 via-obsidian/25 to-transparent"
      />

      <span className="label-caps absolute left-3 top-3 z-10 rounded-full border border-inverse/20 bg-obsidian/70 px-2.5 py-1 text-[10px] text-inverse backdrop-blur-sm">
        MOQ {product.moq}
      </span>

      <div className="absolute inset-x-0 bottom-[-1px] z-10 bg-linear-to-t from-obsidian from-0% via-obsidian/85 via-45% to-transparent px-4 pb-3.5 pt-14">
        <h3
          className={`leading-snug text-inverse transition-colors group-hover:text-brand-soft ${
            tall
              ? "font-accent text-[clamp(1.125rem,2vw,1.5rem)] font-medium italic"
              : "line-clamp-2 text-sm font-medium md:text-base"
          }`}
        >
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
        className="absolute inset-0 z-20 rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-brand focus-visible:ring-offset-2 focus-visible:ring-offset-obsidian"
        aria-label={`View ${product.name}`}
      />
    </motion.article>
  );
}

const BENTO_LAYOUT = [
  { tall: true, className: "col-span-2 md:col-span-5 md:row-span-2 md:row-start-1" },
  { tall: false, className: "col-span-1 md:col-span-4 md:col-start-6 md:row-start-1" },
  { tall: false, className: "col-span-1 md:col-span-3 md:col-start-10 md:row-start-1" },
  { tall: false, className: "col-span-1 md:col-span-4 md:col-start-6 md:row-start-2" },
  { tall: false, className: "col-span-1 md:col-span-3 md:col-start-10 md:row-start-2" },
] as const;

export function PremiumLine() {
  const { products: catalogProducts } = useCatalog();
  const reduced = usePrefersReducedMotion();
  const sectionRef = useRef<HTMLElement>(null);
  const products = getPremiumLineProducts(6, catalogProducts);
  const banner = getBridalBanner();

  const { scrollYProgress } = useScroll({
    target: sectionRef,
    offset: ["start end", "end start"],
  });
  const headerY = useParallaxPx(scrollYProgress, 20, -20);
  const orbY = useParallaxPx(scrollYProgress, -36, 48);
  const orbYAlt = useParallaxPx(scrollYProgress, 40, -32);

  if (products.length === 0) return null;

  const [signature, ...bentoProducts] = products;
  const bentoDepths = [1.15, 0.9, 0.82, 0.78, 0.86];

  return (
    <section
      ref={sectionRef}
      className="relative isolate overflow-hidden bg-obsidian px-margin-mobile py-20 text-inverse md:px-margin-desktop md:py-28"
    >
      <div
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_80%_55%_at_50%_-5%,rgba(184,134,11,0.12),transparent_60%)]"
        aria-hidden
      />
      <div
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_60%_45%_at_100%_80%,rgba(139,41,66,0.1),transparent_55%)]"
        aria-hidden
      />
      <div
        className="hero-grain pointer-events-none absolute inset-0 opacity-[0.03]"
        aria-hidden
      />

      <motion.div
        className="pointer-events-none absolute -right-16 top-1/3 h-80 w-80 rounded-full bg-gold/8 blur-[110px] motion-parallax"
        style={reduced ? undefined : { y: orbY }}
        transformTemplate={parallaxTransformTemplate}
        aria-hidden
      />
      <motion.div
        className="pointer-events-none absolute -left-20 bottom-1/4 h-64 w-64 rounded-full bg-maroon/15 blur-[100px] motion-parallax"
        style={reduced ? undefined : { y: orbYAlt }}
        transformTemplate={parallaxTransformTemplate}
        aria-hidden
      />

      <div className="relative z-10 mx-auto max-w-container-max">
        <motion.header
          className="grid gap-8 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)] lg:items-end lg:gap-14"
          style={reduced ? undefined : { y: headerY }}
          transformTemplate={parallaxTransformTemplate}
          variants={reduced ? undefined : headerContainer}
          initial={reduced ? false : "hidden"}
          whileInView={reduced ? undefined : "show"}
          viewport={{ once: true, margin: "-60px" }}
        >
          <motion.div variants={reduced ? undefined : fadeUp}>
            <p className="label-caps text-gold">Premium wholesale</p>
            <h2 className="mt-4 text-[clamp(1.875rem,4.5vw,4rem)] leading-[1.06] tracking-tight">
              <span className="font-display">The </span>
              <span className="font-accent italic text-inverse/90">
                Premium collection
              </span>
            </h2>
          </motion.div>

          <motion.div
            variants={reduced ? undefined : fadeUp}
            className="flex flex-col gap-6 border-t border-gold/15 pt-8 lg:border-t-0 lg:border-l lg:border-gold/15 lg:pl-12 lg:pt-0"
          >
            <p className="font-accent text-xl leading-relaxed text-inverse-muted italic md:text-2xl md:leading-normal">
              Where craft meets margin — curated occasion pieces for retailers
              who sell on finish, fit, and festival demand.
            </p>
            <Link
              to="/premium"
              className="label-caps group inline-flex w-fit items-center gap-3 border-b border-gold/45 pb-1 text-gold transition-[gap,color] duration-300 hover:gap-4 hover:text-inverse"
            >
              View full catalogue
              <span className="transition-transform group-hover:translate-x-1">
                →
              </span>
            </Link>
          </motion.div>
        </motion.header>

        <CraftMarquee reduced={reduced} />

        <CinemaPortal
          banner={banner}
          scrollYProgress={scrollYProgress}
          reduced={reduced}
        />

        <div className="mt-14 flex items-center gap-4 md:mt-20">
          <span className="h-px flex-1 bg-gold/20" aria-hidden />
          <p className="label-caps shrink-0 text-gold/60">Curated pieces</p>
          <span className="h-px flex-1 bg-gold/20" aria-hidden />
        </div>

        {signature ? (
          <SignaturePiece
            product={signature}
            scrollYProgress={scrollYProgress}
            reduced={reduced}
          />
        ) : null}

        {bentoProducts.length > 0 ? (
          <motion.div
            className="mt-8 grid auto-rows-fr grid-cols-2 gap-4 md:mt-10 md:grid-cols-12 md:grid-rows-2 md:gap-5 lg:gap-6"
            variants={reduced ? undefined : bentoRow}
            initial={reduced ? false : "hidden"}
            whileInView={reduced ? undefined : "show"}
            viewport={{ once: true, margin: "-50px" }}
          >
            {bentoProducts.map((product, i) => {
              const slot = BENTO_LAYOUT[i];
              if (!slot) return null;

              return (
                <div key={product.id} className={`h-full ${slot.className}`}>
                  <BentoCard
                    product={product}
                    scrollYProgress={scrollYProgress}
                    depth={bentoDepths[i] ?? 0.85}
                    reduced={reduced}
                    tall={slot.tall}
                  />
                </div>
              );
            })}
          </motion.div>
        ) : null}

        <motion.footer
          className="mt-16 flex flex-col items-center gap-6 border-t border-gold/15 pt-14 md:mt-24 md:flex-row md:justify-between md:pt-16"
          initial={reduced ? false : { opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.65, ease: ENTRANCE_EASE }}
        >
          <p className="max-w-lg text-center text-sm leading-relaxed text-inverse-muted md:text-left md:text-base">
            Stock the pieces your buyers ask for by name — heavy handwork, bridal
            sets, and premium partywear with consistent grading from Surat &amp;
            Hyderabad.
          </p>
          <Link
            to="/premium"
            className="label-caps inline-flex items-center gap-3 rounded-full border border-gold/30 bg-gold/10 px-6 py-3 text-gold transition-[gap,background-color,border-color] duration-300 hover:gap-4 hover:border-gold/50 hover:bg-gold/15"
          >
            Explore premium line
            <span aria-hidden>→</span>
          </Link>
        </motion.footer>
      </div>
    </section>
  );
}
