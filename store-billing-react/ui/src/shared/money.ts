export function formatMoney(n: number, currency = 'INR') {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(Number.isFinite(n) ? n : 0);
}

export function round2(n: number) {
  return Math.round((n + Number.EPSILON) * 100) / 100;
}
