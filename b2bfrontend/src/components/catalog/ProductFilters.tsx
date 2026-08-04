import { formatPrice } from '@/lib/data'
import type { FilterMeta, ProductFiltersState } from '@/lib/filters'
import { FilterCheckbox } from './FilterCheckbox'
import { FilterSection } from './FilterSection'

interface ProductFiltersProps {
  meta: FilterMeta
  state: ProductFiltersState
  defaults: ProductFiltersState
  onChange: (next: ProductFiltersState) => void
  onReset: () => void
  idPrefix?: string
  /** Hide the built-in title row when the parent shell provides its own header. */
  showHeader?: boolean
}

export function ProductFilters({
  meta,
  state,
  defaults,
  onChange,
  onReset,
  idPrefix = 'filter',
  showHeader = true,
}: ProductFiltersProps) {
  const toggleList = <T extends string | number>(key: keyof ProductFiltersState, value: T, checked: boolean) => {
    const current = state[key] as T[]
    const next = checked ? [...current, value] : current.filter((v) => v !== value)
    onChange({ ...state, [key]: next })
  }

  const priceChanged = state.priceMin > defaults.priceMin || state.priceMax < defaults.priceMax

  return (
    <div className="space-y-0">
      {showHeader ? (
        <div className="flex items-center justify-between pb-3">
          <h2 className="font-display text-xl font-semibold text-ink">Filters</h2>
          <button
            type="button"
            onClick={onReset}
            className="cursor-pointer text-xs font-semibold text-brand hover:text-brand-dark"
          >
            Clear all
          </button>
        </div>
      ) : null}

      <FilterSection title="Price (INR)">
        <div className="space-y-4">
          <div className="flex items-center gap-2">
            <label className="sr-only" htmlFor={`${idPrefix}-price-min`}>
              Minimum price
            </label>
            <input
              id={`${idPrefix}-price-min`}
              type="number"
              min={meta.priceMin}
              max={state.priceMax}
              value={state.priceMin}
              onChange={(e) =>
                onChange({
                  ...state,
                  priceMin: Math.min(Number(e.target.value) || meta.priceMin, state.priceMax),
                })
              }
              className="w-full rounded-xl border border-ink/10 bg-canvas-warm/50 px-3 py-2 text-sm text-ink"
            />
            <span className="text-ink-faint">–</span>
            <label className="sr-only" htmlFor={`${idPrefix}-price-max`}>
              Maximum price
            </label>
            <input
              id={`${idPrefix}-price-max`}
              type="number"
              min={state.priceMin}
              max={meta.priceMax}
              value={state.priceMax}
              onChange={(e) =>
                onChange({
                  ...state,
                  priceMax: Math.max(Number(e.target.value) || meta.priceMax, state.priceMin),
                })
              }
              className="w-full rounded-xl border border-ink/10 bg-canvas-warm/50 px-3 py-2 text-sm text-ink"
            />
          </div>

          <div className="space-y-2">
            <input
              type="range"
              min={meta.priceMin}
              max={meta.priceMax}
              value={state.priceMin}
              onChange={(e) =>
                onChange({
                  ...state,
                  priceMin: Math.min(Number(e.target.value), state.priceMax),
                })
              }
              className="h-1.5 w-full cursor-pointer appearance-none rounded-full bg-ink/10 accent-brand"
            />
            <input
              type="range"
              min={meta.priceMin}
              max={meta.priceMax}
              value={state.priceMax}
              onChange={(e) =>
                onChange({
                  ...state,
                  priceMax: Math.max(Number(e.target.value), state.priceMin),
                })
              }
              className="h-1.5 w-full cursor-pointer appearance-none rounded-full bg-ink/10 accent-brand"
            />
          </div>

          <p className="text-xs text-ink-muted">
            {priceChanged
              ? `${formatPrice(state.priceMin)} – ${formatPrice(state.priceMax)}`
              : `Full range: ${formatPrice(meta.priceMin)} – ${formatPrice(meta.priceMax)}`}
          </p>
        </div>
      </FilterSection>

      {meta.highlightOptions.length > 0 && (
        <FilterSection title="Highlights">
          {meta.highlightOptions.map((item) => (
            <FilterCheckbox
              key={item.value}
              id={`${idPrefix}-highlight-${item.value}`}
              label={item.label}
              checked={state.highlights.includes(item.value)}
              onChange={(checked) => toggleList('highlights', item.value, checked)}
            />
          ))}
        </FilterSection>
      )}

      {meta.moqOptions.length > 1 && (
        <FilterSection title="Minimum order (MOQ)">
          {meta.moqOptions.map((moq) => (
            <FilterCheckbox
              key={moq}
              id={`${idPrefix}-moq-${moq}`}
              label={`${moq} pieces`}
              checked={state.moq.includes(moq)}
              onChange={(checked) => toggleList('moq', moq, checked)}
            />
          ))}
        </FilterSection>
      )}

      {meta.subcategoryOptions.length > 0 && (
        <FilterSection title="Subcategory">
          {meta.subcategoryOptions.map((sub) => (
            <FilterCheckbox
              key={sub.value}
              id={`${idPrefix}-sub-${sub.value}`}
              label={sub.label}
              checked={state.subcategories.includes(sub.value)}
              onChange={(checked) => toggleList('subcategories', sub.value, checked)}
            />
          ))}
        </FilterSection>
      )}

      {meta.fabricOptions.length > 1 && (
        <FilterSection title="Fabric">
          {meta.fabricOptions.map((fabric) => (
            <FilterCheckbox
              key={fabric}
              id={`${idPrefix}-fabric-${fabric}`}
              label={fabric}
              checked={state.fabrics.includes(fabric)}
              onChange={(checked) => toggleList('fabrics', fabric, checked)}
            />
          ))}
        </FilterSection>
      )}

      {meta.bottomOptions.length > 1 && (
        <FilterSection title="Bottom">
          {meta.bottomOptions.map((bottom) => (
            <FilterCheckbox
              key={bottom}
              id={`${idPrefix}-bottom-${bottom}`}
              label={bottom}
              checked={state.bottoms.includes(bottom)}
              onChange={(checked) => toggleList('bottoms', bottom, checked)}
            />
          ))}
        </FilterSection>
      )}

      {meta.dupattaOptions.length > 1 && (
        <FilterSection title="Dupatta">
          {meta.dupattaOptions.map((dupatta) => (
            <FilterCheckbox
              key={dupatta}
              id={`${idPrefix}-dupatta-${dupatta}`}
              label={dupatta}
              checked={state.dupattas.includes(dupatta)}
              onChange={(checked) => toggleList('dupattas', dupatta, checked)}
            />
          ))}
        </FilterSection>
      )}
    </div>
  )
}
