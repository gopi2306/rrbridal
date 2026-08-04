import { useRef } from "react";
import { Link } from "react-router-dom";
import { getFeaturedCategories } from "@/lib/data";
import { SectionHeader } from "@/components/ui/SectionHeader";

export function CategoryScroll() {
  const scrollRef = useRef<HTMLDivElement>(null);
  const categories = getFeaturedCategories(4);

  const scroll = (dir: -1 | 1) => {
    scrollRef.current?.scrollBy({ left: dir * 300, behavior: "smooth" });
  };

  return (
    <section className="section-pad mx-auto max-w-container-max">
      <div className="flex items-end justify-between gap-4 border-b border-outline pb-4">
        <SectionHeader
          label="Catalog"
          title="Browse categories"
          viewAllHref="/salwar-kameez"
          bordered={false}
          className="flex-1"
        />
        <div className="hidden shrink-0 gap-2 sm:flex">
          <button
            type="button"
            onClick={() => scroll(-1)}
            aria-label="Scroll categories left"
            className="flex h-9 w-9 items-center justify-center border border-outline text-ink-muted hover:text-brand"
          >
            ‹
          </button>
          <button
            type="button"
            onClick={() => scroll(1)}
            aria-label="Scroll categories right"
            className="flex h-9 w-9 items-center justify-center border border-outline text-ink-muted hover:text-brand"
          >
            ›
          </button>
        </div>
      </div>

      <div
        ref={scrollRef}
        className="mt-6 flex gap-4 overflow-x-auto hide-scrollbar snap-x snap-mandatory pb-1"
      >
        {categories.map((cat) => (
          <Link
            key={cat.id}
            to={cat.href}
            className="group w-[min(78vw,300px)] shrink-0 snap-start sm:w-[280px]"
          >
            <div className="relative aspect-[3/4] overflow-hidden bg-surface">
              <img
                src={cat.image}
                alt={cat.title}
                loading="lazy"
                className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
              />
              <div className="absolute inset-0 bg-linear-to-t from-canvas/90 via-canvas/10 to-transparent" />
              <div className="absolute bottom-0 left-0 right-0 p-4">
                <p className="text-base font-semibold text-ink">{cat.title}</p>
                <p className="mt-1 line-clamp-2 text-xs text-ink-muted">
                  {cat.description}
                </p>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </section>
  );
}
