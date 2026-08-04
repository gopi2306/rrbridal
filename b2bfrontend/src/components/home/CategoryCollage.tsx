import { useRef } from "react";
import { Link } from "react-router-dom";
import {
  motion,
  useScroll,
  type MotionValue,
} from "framer-motion";
import type { Category } from "@/types";
import { getHomeCollageCategories } from "@/lib/data";
import { OptimizedImage } from "@/components/ui/SafeImage";
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion";
import {
  parallaxTransformTemplate,
  useParallaxPx,
} from "@/hooks/useParallaxPx";

const ENTRANCE_EASE = [0.16, 1, 0.3, 1] as const;

function ParallaxImage({
  cat,
  scrollYProgress,
  depth,
  reduced,
  kenBurns = false,
  eager = false,
  className = "",
  overlayClassName = "",
}: {
  cat: Category;
  scrollYProgress: MotionValue<number>;
  depth: number;
  reduced: boolean;
  kenBurns?: boolean;
  eager?: boolean;
  className?: string;
  overlayClassName?: string;
}) {
  const spread = Math.round(depth * 32);
  const imageY = useParallaxPx(scrollYProgress, -spread, spread);

  return (
    <div className={`relative overflow-hidden ${className}`}>
      <motion.div
        className="absolute top-[-10%] left-0 h-[120%] w-full"
        style={reduced ? undefined : { y: imageY }}
        transformTemplate={parallaxTransformTemplate}
        initial={{ scale: 1 }}
        animate={kenBurns && !reduced ? { scale: 1.07 } : { scale: 1 }}
        transition={{
          scale: { duration: kenBurns && !reduced ? 12 : 0, ease: "linear" },
        }}
      >
        <OptimizedImage
          src={cat.image}
          alt={cat.title}
          loading={eager ? "eager" : "lazy"}
          fetchPriority={eager ? "high" : "auto"}
          className="h-full w-full object-cover object-center transition-transform duration-[1.4s] ease-out group-hover:scale-[1.05]"
        />
      </motion.div>
      {overlayClassName ? (
        <div className={`pointer-events-none absolute inset-0 ${overlayClassName}`} />
      ) : null}
    </div>
  );
}

function FeaturedPortal({
  cat,
  reduced,
  scrollYProgress,
}: {
  cat: Category;
  reduced: boolean;
  scrollYProgress: MotionValue<number>;
}) {
  return (
    <motion.article
      className="group relative mt-12 md:mt-16 lg:mt-20"
      initial={reduced ? false : { opacity: 0, y: 40 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-80px" }}
      transition={{ duration: 0.8, ease: ENTRANCE_EASE }}
    >
      <div className="grid overflow-hidden rounded-2xl border border-outline/45 bg-surface shadow-soft lg:min-h-[min(82vh,680px)] lg:grid-cols-[minmax(0,44%)_minmax(0,56%)]">
        <div className="relative z-10 flex min-h-[320px] flex-col justify-center gap-8 bg-obsidian px-8 py-12 text-inverse sm:px-10 sm:py-14 md:min-h-0 md:px-12 lg:gap-12 lg:px-14 lg:py-20">
          <span className="label-caps inline-flex w-fit rounded-full border border-inverse/20 bg-inverse/10 px-3.5 py-1.5 text-[10px] text-gold backdrop-blur-sm">
            Featured collection
          </span>

          <span className="h-px w-14 bg-gold/45" aria-hidden />

          <div className="space-y-5 md:space-y-6">
            <h3 className="font-accent text-[clamp(2rem,5vw,3.25rem)] font-medium italic leading-[1.06] tracking-tight">
              {cat.title}
            </h3>
            <p className="max-w-md text-base leading-relaxed text-inverse-muted md:text-lg md:leading-relaxed">
              {cat.description}
            </p>
            {cat.children.length > 0 ? (
              <p className="text-sm leading-relaxed text-inverse-faint">
                {cat.children
                  .slice(0, 3)
                  .map((child) => child.label)
                  .join(" · ")}
              </p>
            ) : null}
          </div>

          <span className="label-caps inline-flex items-center gap-3 text-gold transition-[gap] duration-300 group-hover:gap-4">
            Enter collection
            <span aria-hidden>→</span>
          </span>
        </div>

        <ParallaxImage
          cat={cat}
          scrollYProgress={scrollYProgress}
          depth={1.4}
          reduced={reduced}
          kenBurns
          eager
          className="relative min-h-[300px] sm:min-h-[380px] lg:min-h-full"
          overlayClassName="bg-linear-to-t from-obsidian/50 via-obsidian/10 to-transparent lg:bg-linear-to-r lg:from-obsidian/25 lg:via-transparent lg:to-transparent"
        />
      </div>

      <Link
        to={cat.href}
        className="absolute inset-0 z-20 rounded-2xl focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-gold"
        aria-label={`Browse ${cat.title}`}
      />
    </motion.article>
  );
}

function RunwayRow({
  cat,
  stagger,
  reversed,
  reduced,
  scrollYProgress,
  depth,
}: {
  cat: Category;
  stagger: number;
  reversed: boolean;
  reduced: boolean;
  scrollYProgress: MotionValue<number>;
  depth: number;
}) {
  return (
    <motion.article
      className="group relative overflow-hidden rounded-2xl border border-outline/45 bg-surface shadow-soft md:grid md:min-h-[400px] md:grid-cols-2 lg:min-h-[440px]"
      initial={reduced ? false : { opacity: 0, y: 36 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-60px" }}
      transition={{
        duration: 0.75,
        delay: (stagger % 3) * 0.05,
        ease: ENTRANCE_EASE,
      }}
    >
      <ParallaxImage
        cat={cat}
        scrollYProgress={scrollYProgress}
        depth={depth}
        reduced={reduced}
        className={`relative min-h-[260px] sm:min-h-[320px] md:min-h-full ${reversed ? "md:order-2" : ""}`}
        overlayClassName={
          reversed
            ? "bg-linear-to-t from-canvas/80 via-canvas/15 to-transparent md:bg-linear-to-l md:from-canvas/70 md:via-canvas/10 md:to-transparent"
            : "bg-linear-to-t from-canvas/80 via-canvas/15 to-transparent md:bg-linear-to-r md:from-canvas/70 md:via-canvas/10 md:to-transparent"
        }
      />

      <div
        className={`relative flex flex-col justify-center border-outline/30 px-8 py-10 sm:px-10 md:px-12 md:py-14 ${
          reversed ? "md:order-1 md:border-r" : "md:border-l"
        }`}
      >
        <h3 className="font-accent text-[clamp(1.625rem,3.5vw,2.75rem)] font-medium italic leading-[1.08] tracking-tight text-ink">
          {cat.title}
        </h3>
        <p className="mt-5 max-w-md text-base leading-relaxed text-ink-muted md:mt-6">
          {cat.description}
        </p>
        <span className="label-caps mt-10 inline-flex items-center gap-3 text-gold transition-[gap] duration-300 group-hover:gap-4 md:mt-12">
          Shop collection
          <span aria-hidden>→</span>
        </span>
      </div>

      <Link
        to={cat.href}
        className="absolute inset-0 z-10 rounded-2xl focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-gold"
        aria-label={`Browse ${cat.title}`}
      />
    </motion.article>
  );
}

export function CategoryCollage() {
  const reduced = usePrefersReducedMotion();
  const sectionRef = useRef<HTMLElement>(null);
  const cats = getHomeCollageCategories();

  const { scrollYProgress } = useScroll({
    target: sectionRef,
    offset: ["start end", "end start"],
  });
  const headerY = useParallaxPx(scrollYProgress, 24, -24);

  if (cats.length < 6) return null;

  const [featured, ...runway] = cats;
  const runwayDepths = [0.9, 0.78, 1.0, 0.85, 0.95];

  return (
    <section
      ref={sectionRef}
      className="relative isolate overflow-hidden bg-canvas-warm/40 px-margin-mobile py-24 md:px-margin-desktop md:py-32"
    >
      <div
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_80%_50%_at_50%_-10%,rgba(184,134,11,0.07),transparent)]"
        aria-hidden
      />
      <div
        className="hero-grain pointer-events-none absolute inset-0 opacity-[0.022]"
        aria-hidden
      />

      <div className="relative z-10 mx-auto max-w-container-max">
        <motion.header
          className="grid gap-10 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,0.85fr)] lg:items-end lg:gap-16"
          style={reduced ? undefined : { y: headerY }}
          transformTemplate={parallaxTransformTemplate}
          initial={reduced ? false : { opacity: 0 }}
          whileInView={{ opacity: 1 }}
          viewport={{ once: true, margin: "-60px" }}
          transition={{ duration: 0.7, ease: ENTRANCE_EASE }}
        >
          <div>
            <p className="label-caps text-gold">Catalog</p>
            <h2 className="mt-4 text-[clamp(1.875rem,4.5vw,4rem)] leading-[1.08] tracking-tight text-ink">
              <span className="font-display">Browse </span>
              <span className="font-accent italic text-ink/85">categories</span>
            </h2>
          </div>

          <div className="flex flex-col gap-6 border-t border-outline/60 pt-8 lg:border-t-0 lg:border-l lg:pl-12 lg:pt-0">
            <p className="font-accent text-xl leading-relaxed text-ink/75 italic md:text-2xl md:leading-normal">
              A wholesale showroom in six chapters — readymade staples, salwar
              suites, partywear, materials, and premium occasion stock, straight
              from Surat &amp; Hyderabad.
            </p>
            <div className="flex flex-wrap gap-2">
              {["6 collections", "Factory-direct"].map((chip) => (
                <span
                  key={chip}
                  className="label-caps rounded-full border border-outline/70 bg-surface/80 px-3.5 py-1.5 text-[10px] text-ink-muted"
                >
                  {chip}
                </span>
              ))}
            </div>
          </div>
        </motion.header>

        <FeaturedPortal
          cat={featured}
          reduced={reduced}
          scrollYProgress={scrollYProgress}
        />

        <div className="mt-16 flex items-center gap-4 md:mt-24">
          <span className="h-px flex-1 bg-outline/80" aria-hidden />
          <p className="label-caps shrink-0 text-ink-faint">
            More collections
          </p>
          <span className="h-px flex-1 bg-outline/80" aria-hidden />
        </div>

        <div className="mt-10 flex flex-col gap-6 md:mt-12 md:gap-8">
          {runway.map((cat, i) => (
            <RunwayRow
              key={cat.id}
              cat={cat}
              stagger={i}
              reversed={i % 2 === 1}
              reduced={reduced}
              scrollYProgress={scrollYProgress}
              depth={runwayDepths[i] ?? 0.85}
            />
          ))}
        </div>

        <motion.footer
          className="mt-16 flex flex-col items-center gap-6 border-t border-outline/60 pt-14 md:mt-24 md:flex-row md:justify-between md:pt-16"
          initial={reduced ? false : { opacity: 0 }}
          whileInView={{ opacity: 1 }}
          viewport={{ once: true }}
          transition={{ duration: 0.6, ease: ENTRANCE_EASE }}
        >
          <p className="max-w-md text-center text-sm text-ink-muted md:text-left md:text-base">
            Eleven wholesale lines in total — salwar, Pakistani suits, combo
            packs, and more beyond this edit.
          </p>
          <Link
            to="/salwar-kameez"
            className="label-caps group inline-flex items-center gap-3 border-b border-gold/50 pb-1 text-gold transition-[gap,color] duration-300 hover:gap-4 hover:text-ink"
          >
            View full catalog
            <span className="transition-transform group-hover:translate-x-1">
              →
            </span>
          </Link>
        </motion.footer>
      </div>
    </section>
  );
}
