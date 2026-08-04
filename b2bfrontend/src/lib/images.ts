import type { Product } from "@/types";

/**
 * Product photos in public/images/dresses/ (pro-1.webp … pro-12.webp).
 * Each product uses pro-{id}.webp via product.id in resolveIndex().
 *
 * Also:
 *   public/images/hero/hero-01.webp … hero-05.webp  (1920×1080 landscape)
 *   public/images/banners/banner-01.webp, banner-2.webp
 *
 * Drop new photos as .jpg, then run: npm run compress:images
 */

const DRESS_IMAGES = [
  "/images/dresses/pro-1.webp",
  "/images/dresses/pro-2.webp",
  "/images/dresses/pro-3.webp",
  "/images/dresses/pro-4.webp",
  "/images/dresses/pro-5.webp",
  "/images/dresses/pro-6.webp",
  "/images/dresses/pro-7.webp",
  "/images/dresses/pro-8.webp",
  "/images/dresses/pro-9.webp",
  "/images/dresses/pro-10.webp",
  "/images/dresses/pro-11.webp",
  "/images/dresses/pro-12.webp",
] as const;

export const HERO_PATHS = [
  "/images/hero/hero-01.webp",
  "/images/hero/hero-02.webp",
  "/images/hero/hero-03.webp",
  "/images/hero/hero-04.webp",
  "/images/hero/hero-05.webp",
] as const;

export const HERO_FALLBACKS = [
  DRESS_IMAGES[2],
  DRESS_IMAGES[3],
  DRESS_IMAGES[10],
  DRESS_IMAGES[1],
  DRESS_IMAGES[4],
] as const;

export const BANNER_PATHS = {
  summer: "/images/banners/banner-01.webp",
  bridal: "/images/banners/banner-2.webp",
} as const;

export const BANNER_FALLBACKS = {
  summer: DRESS_IMAGES[3],
  bridal: DRESS_IMAGES[10],
} as const;

function resolveIndex(product: Product): number {
  const hash = [...product.id].reduce((total, char) => total + char.charCodeAt(0), 0);
  return hash % DRESS_IMAGES.length;
}

export function getProductImage(product: Product | number): string {
  if (typeof product === "number") {
    return DRESS_IMAGES[product % DRESS_IMAGES.length];
  }
  return product.images?.[0] || DRESS_IMAGES[resolveIndex(product)];
}

export function getProductGallery(product: Product): string[] {
  if (product.images?.length) return product.images;
  const base = resolveIndex(product);
  const a = DRESS_IMAGES[base];
  const b = DRESS_IMAGES[(base + 1) % DRESS_IMAGES.length];
  const c = DRESS_IMAGES[(base + 2) % DRESS_IMAGES.length];
  return [...new Set([a, b, c])];
}

export interface HeroSlide {
  src: string;
  fallback: string;
  alt: string;
  label: string;
  headlineLead: string;
  headlineAccent: string;
  subtitle: string;
  href: string;
  /** object-cover focal point — use % for fine control (e.g. "50% 5%") */
  objectPosition?: string;
  /** Zoom out inside the frame to reveal headroom (e.g. 0.9) */
  imageScale?: number;
  /** transform-origin for imageScale (e.g. "50% 15%") */
  transformOrigin?: string;
}

const HERO_META: Omit<HeroSlide, "src" | "fallback">[] = [
  {
    alt: "Readymade kurti wholesale collection",
    label: "Readymade Kurtis",
    headlineLead: "Wholesale trade",
    headlineAccent: "Readymade Kurtis",
    subtitle:
      "Ready-to-ship kurti sets for boutiques and multi-brand stores. MOQ from 4 pieces.",
    href: "/readymade",
  },
  {
    alt: "Partywear and festive wholesale",
    label: "Partywear",
    headlineLead: "Festive season",
    headlineAccent: "Partywear",
    subtitle:
      "Gowns, lehengas and occasion wear your buyers reach for at peak season.",
    href: "/partywear",
  },
  {
    alt: "Premium bridal wholesale collection",
    label: "Bridal & Premium",
    headlineLead: "Occasion wear",
    headlineAccent: "Bridal & Premium",
    subtitle:
      "Handwork-rich bridal and premium pieces built for high-ticket retail.",
    href: "/premium",
  },
  {
    alt: "Pakistani suits bulk wholesale",
    label: "Pakistani Suits",
    headlineLead: "Bulk orders",
    headlineAccent: "Pakistani Suits",
    subtitle:
      "Trend-led Pakistani cuts with consistent grading for repeat wholesale buyers.",
    href: "/pakistani-suits",
  },
  {
    alt: "Salwar kameez wholesale staples",
    label: "Salwar Kameez",
    headlineLead: "Everyday catalogue",
    headlineAccent: "Salwar Kameez",
    subtitle:
      "Core ethnic staples retailers restock year-round — priced for volume trade.",
    href: "/salwar-kameez",
  },
];

export function getHeroSlides(): HeroSlide[] {
  return HERO_META.map((meta, i) => ({
    ...meta,
    src: HERO_PATHS[i],
    fallback: HERO_FALLBACKS[i],
  }));
}

export function getCampaignBanner(): { src: string; fallback: string } {
  return { src: BANNER_PATHS.summer, fallback: BANNER_FALLBACKS.summer };
}

export function getBridalBanner(): { src: string; fallback: string } {
  return { src: BANNER_PATHS.bridal, fallback: BANNER_FALLBACKS.bridal };
}

export function getHeroImage(): string {
  return HERO_PATHS[0];
}

export function getBridalImage(): string {
  return BANNER_PATHS.bridal;
}

