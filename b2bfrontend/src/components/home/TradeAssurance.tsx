import { Link } from "react-router-dom";
import { about } from "@/data/about";
import site from "@/data/site";
import { FadeIn } from "@/components/ui/FadeIn";
import { CountUp } from "@/components/ui/CountUp";

const VALUE_ICONS = [
  <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden>
    <path d="M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h6v6h-6v-6z" />
  </svg>,
  <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden>
    <path d="M12 2l2.4 7.4H22l-6.2 4.5 2.4 7.4L12 17l-6.2 4.3 2.4-7.4L2 9.4h7.6L12 2z" />
  </svg>,
  <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden>
    <path d="M19 4h-1V2h-2v2H8V2H6v2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2zm0 16H5V10h14v10zM7 12h2v2H7v-2zm4 0h2v2h-2v-2zm4 0h2v2h-2v-2z" />
  </svg>,
  <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden>
    <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.22.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z" />
  </svg>,
] as const;

export function TradeAssurance() {
  return (
    <section className="section-pad">
      <div className="mx-auto max-w-container-max">
        <div className="overflow-hidden rounded-2xl border border-outline/60 bg-surface-high/40 shadow-soft backdrop-blur-sm">
          <div className="border-b border-outline/40 px-4 py-8 text-center sm:px-6 md:px-10 md:py-14">
            <FadeIn>
              <p className="label-caps text-brand">Our promise</p>
              <h2 className="mx-auto mt-4 max-w-3xl text-xl font-semibold leading-snug text-ink sm:text-2xl md:text-4xl">
                {site.slogan}
              </h2>
              <p className="mx-auto mt-5 max-w-2xl text-sm leading-relaxed text-ink-muted md:text-base">
                {about.mission}
              </p>
            </FadeIn>
          </div>

          <div className="grid grid-cols-2 divide-x divide-y divide-outline/30 border-b border-outline/30 sm:grid-cols-2 md:grid-cols-4 md:divide-y-0">
            {about.highlights.map((item, i) => (
              <FadeIn
                key={item.title}
                delay={0.04 * i}
                className="px-3 py-6 text-center sm:px-5 sm:py-8 md:px-6 md:py-10"
              >
                <p className="text-xl font-semibold tabular-nums text-ink sm:text-2xl md:text-3xl">
                  <CountUp value={item.title} />
                </p>
                <p className="mt-2 text-xs text-ink-muted md:text-sm">
                  {item.desc}
                </p>
              </FadeIn>
            ))}
          </div>

          <div className="grid gap-px bg-outline/30 md:grid-cols-2 lg:grid-cols-4">
            {about.values.map((value, i) => (
              <FadeIn
                key={value.title}
                delay={0.05 * i}
                className="bg-surface-high/60 px-4 py-6 sm:px-6 sm:py-8 md:px-7 md:py-9"
              >
                <div
                  className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand text-white shadow-[0_8px_20px_-8px_rgba(233,30,140,0.65)]"
                  aria-hidden
                >
                  {VALUE_ICONS[i % VALUE_ICONS.length]}
                </div>
                <h3 className="mt-4 text-sm font-semibold text-ink md:text-base">
                  {value.title}
                </h3>
                <p className="mt-2 text-sm leading-relaxed text-ink-muted">
                  {value.desc}
                </p>
              </FadeIn>
            ))}
          </div>

          <div className="px-4 py-6 text-center sm:px-6 md:px-10 md:py-8">
            <FadeIn delay={0.2}>
              <p className="text-sm text-ink-muted">
                Factory-direct wholesale from {site.locationNote} — quality
                checked, bulk-ready, trade-first.
              </p>
              <Link
                to="/about"
                className="label-caps mt-5 inline-flex text-ink-muted transition-colors hover:text-brand"
              >
                Learn about {site.name} →
              </Link>
            </FadeIn>
          </div>
        </div>
      </div>
    </section>
  );
}
