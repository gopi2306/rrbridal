interface FilterCheckboxProps {
  id: string
  label: string
  checked: boolean
  count?: number
  onChange: (checked: boolean) => void
}

export function FilterCheckbox({ id, label, checked, count, onChange }: FilterCheckboxProps) {
  return (
    <label htmlFor={id} className="flex cursor-pointer items-center gap-3 rounded-lg py-1.5 text-sm text-ink-muted transition-colors hover:text-ink">
      <input
        id={id}
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="h-4 w-4 cursor-pointer rounded border-ink/20 text-brand focus:ring-brand/30"
      />
      <span className="flex-1">{label}</span>
      {count !== undefined && <span className="text-xs text-ink-faint">{count}</span>}
    </label>
  )
}
