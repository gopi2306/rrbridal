import { roundMoney } from '../../common/money.util';

export const PRODUCT_GST_INCLUSIVE_PRICE_FIELDS = [
  'costPrice',
  'mrp',
  'sellingPrice',
  'storePrice',
] as const;

export const PRODUCT_WITHOUT_GST_PRICE_FIELDS = [
  'costPriceWithoutGst',
  'mrpWithoutGst',
  'sellingPriceWithoutGst',
  'storePriceWithoutGst',
] as const;

const PRODUCT_PRICE_FIELD_PAIRS = [
  ['costPrice', 'costPriceWithoutGst'],
  ['mrp', 'mrpWithoutGst'],
  ['sellingPrice', 'sellingPriceWithoutGst'],
  ['storePrice', 'storePriceWithoutGst'],
] as const;

export type ProductGstInclusivePriceField = (typeof PRODUCT_GST_INCLUSIVE_PRICE_FIELDS)[number];
export type ProductWithoutGstPriceField = (typeof PRODUCT_WITHOUT_GST_PRICE_FIELDS)[number];

export type ProductPriceSource = Partial<Record<ProductGstInclusivePriceField, unknown>> & {
  gstPercent?: unknown;
};

export type ProductWithoutGstPrices = Partial<Record<ProductWithoutGstPriceField, number>>;

export function priceWithoutGst(inclusivePrice: number, gstPercent: number): number {
  return roundMoney(inclusivePrice / (1 + gstPercent / 100));
}

export function deriveWithoutGstPrices(source: ProductPriceSource): ProductWithoutGstPrices | null {
  const gstPercent = source.gstPercent;
  if (
    typeof gstPercent !== 'number' ||
    !Number.isFinite(gstPercent) ||
    gstPercent < 0 ||
    gstPercent > 100
  ) {
    return null;
  }

  const result: ProductWithoutGstPrices = {};
  for (const [sourceField, targetField] of PRODUCT_PRICE_FIELD_PAIRS) {
    const value = source[sourceField];
    if (typeof value === 'number' && Number.isFinite(value)) {
      result[targetField] = priceWithoutGst(value, gstPercent);
    }
  }
  return result;
}

export function hasWithoutGstPriceSourceChanged(patch: Record<string, unknown>): boolean {
  return (
    'gstPercent' in patch ||
    PRODUCT_GST_INCLUSIVE_PRICE_FIELDS.some((field) => field in patch)
  );
}
