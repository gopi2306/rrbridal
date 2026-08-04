import type { ProductFiltersState, SortOption } from '@/lib/filters'
import { SORT_LABELS } from '@/lib/filters'

interface CatalogToolbarProps {
  total: number
  filtered: number
  activeFilters: number
  sort: SortOption
  onSortChange: (sort: SortOption) => void
  onOpenFilters: () => void
}

export function CatalogToolbar({
  total,
  filtered,
  activeFilters,
  sort,
  onSortChange,
  onOpenFilters,
}: CatalogToolbarProps) {
  return (
    <div className="mb-8 flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
      <div>
        <p className="text-sm text-ink-muted">
          Showing <span className="font-semibold text-ink">{filtered}</span> of{' '}
          <span className="font-semibold text-ink">{total}</span> products
        </p>
      </div>

      <div className="flex w-full flex-wrap items-center gap-3 sm:w-auto">
        <button
          type="button"
          onClick={onOpenFilters}
          className="inline-flex cursor-pointer items-center gap-2 rounded-full border border-ink/10 bg-surface px-4 py-2.5 text-sm font-medium text-ink transition-colors hover:border-brand hover:text-brand lg:hidden"
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M4 6h16M7 12h10M10 18h4" />
          </svg>
          Filters
          {activeFilters > 0 && (
            <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-brand px-1.5 text-[10px] font-bold text-white">
              {activeFilters}
            </span>
          )}
        </button>

        <label htmlFor="catalog-sort" className="sr-only">
          Sort products
        </label>
        <select
          id="catalog-sort"
          value={sort}
          onChange={(e) => onSortChange(e.target.value as SortOption)}
          className="w-full min-w-0 cursor-pointer rounded-full border border-ink/10 bg-surface px-4 py-2.5 text-sm font-medium text-ink transition-colors hover:border-brand focus:border-brand focus:outline-none sm:w-auto"
        >
          {(Object.keys(SORT_LABELS) as SortOption[]).map((key) => (
            <option key={key} value={key}>
              {SORT_LABELS[key]}
            </option>
          ))}
        </select>
      </div>
    </div>
  )
}

interface ActiveFilterChipsProps {
  state: ProductFiltersState
  defaults: ProductFiltersState
  meta: import('@/lib/filters').FilterMeta
  onChange: (next: ProductFiltersState) => void
  onReset: () => void
}

export function ActiveFilterChips({ state, defaults, meta, onChange, onReset }: ActiveFilterChipsProps) {
  const chips: { key: string; label: string; onRemove: () => void }[] = []

  if (state.priceMin > defaults.priceMin || state.priceMax < defaults.priceMax) {
    chips.push({
      key: 'price',
      label: `₹${state.priceMin} – ₹${state.priceMax}`,
      onRemove: () => onChange({ ...state, priceMin: defaults.priceMin, priceMax: defaults.priceMax }),
    })
  }

  state.moq.forEach((moq) => {
    chips.push({
      key: `moq-${moq}`,
      label: `MOQ ${moq}`,
      onRemove: () => onChange({ ...state, moq: state.moq.filter((m) => m !== moq) }),
    })
  })

  state.subcategories.forEach((sub) => {
    const label = meta.subcategoryOptions.find((o) => o.value === sub)?.label ?? sub
    chips.push({
      key: `sub-${sub}`,
      label,
      onRemove: () =>
        onChange({ ...state, subcategories: state.subcategories.filter((s) => s !== sub) }),
    })
  })

  state.fabrics.forEach((fabric) => {
    chips.push({
      key: `fabric-${fabric}`,
      label: fabric,
      onRemove: () => onChange({ ...state, fabrics: state.fabrics.filter((f) => f !== fabric) }),
    })
  })

  state.highlights.forEach((highlight) => {
    const labels = { summer: 'Summer picks', month: 'Product of the month', new: 'New arrivals' }
    chips.push({
      key: `highlight-${highlight}`,
      label: labels[highlight],
      onRemove: () =>
        onChange({ ...state, highlights: state.highlights.filter((h) => h !== highlight) }),
    })
  })

  if (!chips.length) return null

  return (
    <div className="mb-6 flex flex-wrap items-center gap-2">
      {chips.map((chip) => (
        <button
          key={chip.key}
          type="button"
          onClick={chip.onRemove}
          className="inline-flex cursor-pointer items-center gap-1.5 rounded-full border border-brand/20 bg-brand-soft/60 px-3 py-1.5 text-xs font-medium text-brand-dark transition-colors hover:bg-brand-soft"
        >
          {chip.label}
          <span aria-hidden>×</span>
        </button>
      ))}
      <button
        type="button"
        onClick={onReset}
        className="cursor-pointer text-xs font-semibold text-ink-muted hover:text-brand"
      >
        Clear all
      </button>
    </div>
  )
}
