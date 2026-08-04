import { useEffect, useMemo, useRef } from "react";
import { testimonials } from "@/data/testimonials";
import type { Testimonial } from "@/types";
import { FadeIn } from "@/components/ui/FadeIn";
import { usePrefersReducedMotion } from "@/hooks/usePrefersReducedMotion";

const SCROLL_SPEED = 0.45;

function initials(name: string): string {
  return name
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();
}

function VoiceCard({ testimonial }: { testimonial: Testimonial }) {
  return (
    <article className="flex h-[20rem] w-[min(88vw,340px)] shrink-0 flex-col rounded-2xl border border-outline/50 bg-surface-high/50 p-5 backdrop-blur-sm sm:h-[23rem] sm:w-[360px] sm:p-6">
      <div className="flex min-h-0 flex-1 flex-col">
        <div className="flex shrink-0 gap-0.5 text-brand" aria-hidden>
          {Array.from({ length: 5 }).map((_, i) => (
            <span key={i} className="text-sm">
              ★
            </span>
          ))}
        </div>
        <p className="mt-4 line-clamp-[10] text-sm leading-relaxed text-ink-muted md:text-base md:leading-relaxed">
          &ldquo;{testimonial.text}&rdquo;
        </p>
      </div>

      <div className="mt-5 flex shrink-0 items-center gap-3 border-t border-outline/30 pt-5">
        <div
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-brand/15 text-xs font-semibold text-brand"
          aria-hidden
        >
          {initials(testimonial.name)}
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-ink">
            {testimonial.name}
          </p>
          {testimonial.role && (
            <p className="truncate text-xs text-ink-faint">
              {testimonial.role}
            </p>
          )}
        </div>
      </div>
    </article>
  );
}

export function RetailVoices() {
  const reduced = usePrefersReducedMotion();
  const scrollRef = useRef<HTMLDivElement>(null);
  const pausedRef = useRef(false);
  const resumeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loopedTestimonials = useMemo(
    () => [...testimonials, ...testimonials],
    [],
  );

  const pause = () => {
    pausedRef.current = true;
    if (resumeTimerRef.current) {
      clearTimeout(resumeTimerRef.current);
      resumeTimerRef.current = null;
    }
  };

  const scheduleResume = (delayMs = 0) => {
    if (resumeTimerRef.current) clearTimeout(resumeTimerRef.current);
    resumeTimerRef.current = setTimeout(() => {
      pausedRef.current = false;
      resumeTimerRef.current = null;
    }, delayMs);
  };

  const scroll = (dir: -1 | 1) => {
    pause();
    scrollRef.current?.scrollBy({ left: dir * 380, behavior: "smooth" });
    scheduleResume(2500);
  };

  useEffect(() => {
    if (reduced) return;

    const el = scrollRef.current;
    const section = el?.closest("section");
    if (!el || !section) return;

    let rafId = 0;
    let inView = true;

    const observer = new IntersectionObserver(
      ([entry]) => {
        inView = entry.isIntersecting;
      },
      { rootMargin: "100px" },
    );
    observer.observe(section);

    const tick = () => {
      if (inView && !pausedRef.current) {
        el.scrollLeft += SCROLL_SPEED;
        const loopWidth = el.scrollWidth / 2;
        if (loopWidth > 0 && el.scrollLeft >= loopWidth) {
          el.scrollLeft -= loopWidth;
        }
      }
      rafId = requestAnimationFrame(tick);
    };

    rafId = requestAnimationFrame(tick);

    return () => {
      observer.disconnect();
      cancelAnimationFrame(rafId);
      if (resumeTimerRef.current) clearTimeout(resumeTimerRef.current);
    };
  }, [reduced]);

  return (
    <section className="relative section-pad overflow-hidden">
      <div
        className="pointer-events-none absolute -right-32 top-0 h-96 w-96 rounded-full bg-brand/6 blur-[120px]"
        aria-hidden
      />

      <div className="relative mx-auto max-w-container-max">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <FadeIn className="max-w-2xl">
            <p className="label-caps text-brand">Retail voices</p>
            <h2 className="mt-3 text-2xl font-semibold tracking-tight text-ink md:text-3xl">
              Trusted by boutique owners &amp; wholesalers
            </h2>
            <p className="mt-3 text-sm leading-relaxed text-ink-muted md:text-base">
              Real feedback from trade partners who stock, reorder, and grow
              with us.
            </p>
          </FadeIn>

          <div className="flex shrink-0 gap-2 self-end sm:self-auto">
            <button
              type="button"
              onClick={() => scroll(-1)}
              aria-label="Scroll testimonials left"
              className="flex h-9 w-9 items-center justify-center border border-outline/60 text-ink-muted transition-colors hover:border-brand hover:text-brand"
            >
              ‹
            </button>
            <button
              type="button"
              onClick={() => scroll(1)}
              aria-label="Scroll testimonials right"
              className="flex h-9 w-9 items-center justify-center border border-outline/60 text-ink-muted transition-colors hover:border-brand hover:text-brand"
            >
              ›
            </button>
          </div>
        </div>

        <FadeIn className="mt-10">
          <div
            ref={scrollRef}
            className="flex items-stretch gap-4 overflow-x-auto hide-scrollbar pb-2 md:gap-5"
            onMouseEnter={pause}
            onMouseLeave={() => scheduleResume()}
            onPointerDown={pause}
            onPointerUp={() => scheduleResume(1800)}
            onTouchStart={pause}
            onTouchEnd={() => scheduleResume(1800)}
            onWheel={() => {
              pause();
              scheduleResume(2000);
            }}
            aria-label="Customer testimonials"
          >
            {loopedTestimonials.map((t, i) => (
              <VoiceCard key={`${t.id}-${i}`} testimonial={t} />
            ))}
          </div>
        </FadeIn>
      </div>
    </section>
  );
}
