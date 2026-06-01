export const MONEY_DECIMAL_PLACES = 4;

export function roundMoney(value: number): number {
  const f = 10 ** MONEY_DECIMAL_PLACES;
  return Math.round((value + Number.EPSILON) * f) / f;
}

export function formatMoney(value: number): string {
  return roundMoney(value).toFixed(MONEY_DECIMAL_PLACES);
}

export function formatMoneyOrEmpty(value: unknown): string {
  if (typeof value !== 'number' || !Number.isFinite(value)) return '';
  return formatMoney(value);
}
