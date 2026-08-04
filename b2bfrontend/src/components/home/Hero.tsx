import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  AnimatePresence,
  motion,
  useScroll,
  useTransform,
} from "framer-motion";
import site from "@/data/site";
import { getHeroSlides } from "@/lib/images";
import { SafeImage } from "@/components/ui/SafeImage";
import { HeroSlideIndicators } from "@/components/home/HeroSlideIndicators";
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion";
import {
  parallaxTransformTemplate,
  useParallaxPx,
} from "@/hooks/useParallaxPx";
import { generalEnquiryUrl } from "@/lib/whatsapp";

const INTERVAL_MS = 2800;
const FADE_MS = 750;
const FADE_EASE = [0.22, 1, 0.36, 1] as const;

const TRUST_CHIPS = [
  "4-piece MOQ",
  "Surat & Hyderabad",
  "Factory-direct pricing",
] as const;

interface HeroBackgroundImageProps {
  src: string;
  fallback: string;
  alt: string;
  eager?: boolean;
}

function HeroBackgroundImage({
  src,
  fallback,
  alt,
  eager,
}: HeroBackgroundImageProps) {
  return (
    <SafeImage
      src={src}
      fallback={fallback}
      alt={alt}
      loading={eager ? "eager" : "lazy"}
      decoding="async"
      fetchPriority={eager ? "high" : "auto"}
      className="h-full w-full min-h-full min-w-full object-cover object-center"
    />
  );
}

export function Hero() {
  const reduced = usePrefersReducedMotion();
  const sectionRef = useRef<HTMLElement>(null);
  const slides = getHeroSlides();
  const [index, setIndex] = useState(0);
  const [prevIndex, setPrevIndex] = useState(0);
  const [autoplayEpoch, setAutoplayEpoch] = useState(0);

  const { scrollYProgress } = useScroll({
    target: sectionRef,
    offset: ["start start", "end start"],
  });
  const parallaxY = useParallaxPx(scrollYProgress, 0, 140);
  const contentY = useParallaxPx(scrollYProgress, 0, 40);
  const contentOpacity = useTransform(scrollYProgress, (v) => {
    const progress = Math.min(1, v / 0.75);
    return Math.round(Math.max(0.4, 1 - progress * 0.6) * 100) / 100;
  });

  const goTo = useCallback((i: number) => {
    setIndex(i);
    setAutoplayEpoch((n) => n + 1);
  }, []);

  useEffect(() => {
    slides.forEach((s) => {
      const img = new Image();
      img.src = s.src;
      img.onerror = () => {
        const fb = new Image();
        fb.src = s.fallback;
      };
    });
  }, [slides]);

  useEffect(() => {
    if (reduced) {
      setPrevIndex(index);
      return;
    }
    const id = window.setTimeout(() => setPrevIndex(index), FADE_MS);
    return () => window.clearTimeout(id);
  }, [index, reduced]);

  useEffect(() => {
    if (reduced) return;
    const id = window.setInterval(
      () => setIndex((i) => (i + 1) % slides.length),
      INTERVAL_MS,
    );
    return () => window.clearInterval(id);
  }, [reduced, slides.length, autoplayEpoch]);

  const slide = slides[index];

  return (
    <section
      ref={sectionRef}
      className="relative min-h-svh w-full overflow-hidden bg-obsidian md:min-h-0 md:h-[min(100svh,56.25vw)]"
    >
      {/* Imagery */}
      <motion.div
        className="motion-parallax absolute inset-0"
        style={reduced ? undefined : { y: parallaxY }}
        transformTemplate={parallaxTransformTemplate}
      >
        {slides.map((s, i) => {
          const isActive = i === index;
          const isUnder = i === prevIndex && !isActive;
          const visible = isActive || isUnder;

          return (
            <motion.div
              key={s.src}
              className="absolute inset-0 overflow-hidden"
              initial={false}
              animate={{ opacity: visible ? 1 : 0 }}
              transition={{
                opacity: {
                  duration: isActive && !reduced ? FADE_MS / 1000 : 0,
                  ease: FADE_EASE,
                },
              }}
              style={{ zIndex: isActive ? 2 : isUnder ? 1 : 0 }}
              aria-hidden={!isActive}
            >
              <motion.div
                className="h-full w-full"
                initial={{ scale: 1 }}
                animate={{
                  scale: isActive && !reduced ? 1.08 : isUnder ? 1.08 : 1,
                }}
                transition={{
                  scale: {
                    duration: isActive && !reduced ? INTERVAL_MS / 1000 : 0,
                    ease: "linear",
                  },
                }}
                style={{ transformOrigin: "50% 50%" }}
              >
                <HeroBackgroundImage
                  src={s.src}
                  fallback={s.fallback}
                  alt={s.alt}
                  eager
                />
              </motion.div>
            </motion.div>
          );
        })}
      </motion.div>

      {/* Cinematic atmosphere */}
      <div className="pointer-events-none absolute inset-0 z-10 bg-linear-to-t from-obsidian via-obsidian/35 to-obsidian/15" />
      <div className="pointer-events-none absolute inset-0 z-10 bg-linear-to-r from-obsidian/75 via-obsidian/15 to-obsidian/45" />
      <div
        className="pointer-events-none absolute -right-20 top-1/4 z-10 h-80 w-80 rounded-full bg-brand/35 blur-[100px]"
        aria-hidden
      />
      <div
        className="pointer-events-none absolute -left-32 bottom-0 z-10 h-64 w-64 rounded-full bg-maroon/25 blur-[90px]"
        aria-hidden
      />
      <div
        className="hero-grain pointer-events-none absolute inset-0 z-10"
        aria-hidden
      />

      {/* Content */}
      <motion.div
        className="motion-parallax absolute inset-0 z-20 flex min-h-svh flex-col md:min-h-0"
        style={reduced ? undefined : { y: contentY, opacity: contentOpacity }}
        transformTemplate={parallaxTransformTemplate}
      >
        <div className="mx-auto flex w-full max-w-container-max flex-1 items-center px-margin-mobile md:pt-12 md:px-margin-desktop">
          <div className="max-w-2xl text-left lg:max-w-3xl">
            <div className="mb-5 flex flex-wrap gap-2 sm:mb-6">
              {TRUST_CHIPS.map((chip) => (
                <span
                  key={chip}
                  className="label-caps rounded-full border border-inverse/15 bg-inverse/10 px-3 py-1.5 text-[10px] text-inverse/90 backdrop-blur-md"
                >
                  {chip}
                </span>
              ))}
            </div>

            <AnimatePresence mode="wait">
              <motion.div
                key={slide.headlineAccent}
                initial={reduced ? false : { opacity: 0, y: 24 }}
                animate={{ opacity: 1, y: 0 }}
                exit={reduced ? undefined : { opacity: 0, y: -12 }}
                transition={{ duration: 0.5, ease: FADE_EASE }}
              >
                <div className="flex items-center gap-3">
                  <span className="h-px w-10 bg-brand" aria-hidden />
                  <p className="label-caps text-inverse-muted">
                    {site.hero.eyebrow}
                  </p>
                </div>

                <h1 className="mt-4 md:mt-5">
                  <span className="block font-display text-lg font-light tracking-wide text-inverse/75 sm:text-xl">
                    {slide.headlineLead}
                  </span>
                  <span className="hero-title-accent mt-1 block pb-1 font-accent text-[3rem] italic leading-[1.08] tracking-tight sm:text-7xl md:text-8xl lg:text-[5.5rem]">
                    {slide.headlineAccent}
                  </span>
                </h1>

                <p className="mt-4 max-w-lg text-sm leading-relaxed text-inverse-muted sm:text-base md:mt-5 md:text-lg">
                  {slide.subtitle}
                </p>
              </motion.div>
            </AnimatePresence>

            <div className="mt-7 flex flex-wrap items-center gap-3 sm:mt-8">
              <Link
                to={slide.href}
                className="label-caps inline-flex items-center gap-2 rounded-full bg-brand px-7 py-3.5 text-xs text-white shadow-[0_8px_32px_-6px_rgba(233,30,140,0.65)] transition-all hover:bg-brand-dark hover:shadow-[0_12px_40px_-6px_rgba(233,30,140,0.75)] sm:text-sm"
              >
                {site.hero.primaryCta}
                <span aria-hidden>→</span>
              </Link>
              <a
                href={generalEnquiryUrl()}
                target="_blank"
                rel="noopener noreferrer"
                className="label-caps inline-flex items-center gap-2 rounded-full border border-inverse/15 bg-inverse/10 px-6 py-3.5 text-xs text-inverse/90 backdrop-blur-md transition-colors hover:border-inverse/25 hover:bg-inverse/15 sm:text-sm"
              >
                {site.hero.secondaryCta}
              </a>
            </div>
          </div>
        </div>

        <div className="flex justify-center pb-6 md:pb-8">
          <HeroSlideIndicators
            slides={slides}
            index={index}
            onSelect={goTo}
            reducedMotion={reduced}
            variant="inverse"
            showCount={false}
          />
        </div>
      </motion.div>
    </section>
  );
}
