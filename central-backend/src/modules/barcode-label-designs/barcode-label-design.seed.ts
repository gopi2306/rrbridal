import type { BarcodeFieldStyleKey } from './barcode-label-design.types';

export const BARCODE_PRINTER_PROFILE_SEEDS = [
  {
    profileId: 'tsc-ttp-244-pro',
    name: 'TSC TTP-244 Pro / TTP-345',
    manufacturer: 'TSC',
    dpi: 203,
    labelWidthMm: 50,
    labelHeightMm: 38,
    labelsPerRow: 2,
  },
  {
    profileId: 'tvs-lp46-neo',
    name: 'TVS LP 46 NEO',
    manufacturer: 'TVS',
    dpi: 203,
    labelWidthMm: 38,
    labelHeightMm: 33,
    labelsPerRow: 2,
  },
  {
    profileId: 'zebra-gk420t',
    name: 'Zebra GK420t',
    manufacturer: 'Zebra',
    dpi: 203,
    labelWidthMm: 50,
    labelHeightMm: 25,
    labelsPerRow: 2,
  },
  {
    profileId: 'godex-g500',
    name: 'Godex G500',
    manufacturer: 'Godex',
    dpi: 203,
    labelWidthMm: 50,
    labelHeightMm: 25,
    labelsPerRow: 2,
  },
  {
    profileId: 'citizen-cl-s621',
    name: 'Citizen CL-S621',
    manufacturer: 'Citizen',
    dpi: 203,
    labelWidthMm: 50,
    labelHeightMm: 25,
    labelsPerRow: 2,
  },
  {
    profileId: 'generic-thermal',
    name: 'Generic Windows Thermal',
    manufacturer: 'Generic',
    dpi: 203,
    labelWidthMm: 50,
    labelHeightMm: 25,
    labelsPerRow: 1,
  },
] as const;

export const DEFAULT_RETAIL_STACKED_FIELD_STYLES: Record<
  BarcodeFieldStyleKey,
  { sizePt: number; weight: 'regular' | 'bold' }
> = {
  productName: { sizePt: 6, weight: 'bold' },
  designSku: { sizePt: 5.5, weight: 'bold' },
  sellingPrice: { sizePt: 5.5, weight: 'bold' },
  sizeNote: { sizePt: 5.5, weight: 'bold' },
  batchNumber: { sizePt: 5.5, weight: 'regular' },
  expiryDate: { sizePt: 5.5, weight: 'regular' },
  brandName: { sizePt: 5.5, weight: 'bold' },
  barcodeNumber: { sizePt: 7, weight: 'bold' },
};

export const DEFAULT_RETAIL_STACKED_DESIGN = {
  name: 'Retail stacked (default)',
  isActive: true,
  layoutStyle: 'retail_stacked' as const,
  printerProfileId: 'tsc-ttp-244-pro',
  labelWidthMm: 50,
  labelHeightMm: 38,
  labelsPerRow: 2,
  dpi: 203,
  fields: {
    productName: true,
    designSku: true,
    sellingPrice: true,
    sizeNote: true,
    batchNumber: false,
    expiryDate: false,
    brandName: false,
  },
  text: {
    productNameSource: 'itemName' as const,
    designNoPrefix: 'D.No:',
    pricePrefix: 'Price ₹:',
    notePrefix: 'Note:',
    priceStyle: 'whole' as const,
    barcodeHumanText: 'sku_spaced' as const,
    alignment: 'center' as const,
  },
  barcode: {
    heightMm: 12,
    widthMm: 42,
  },
  styles: DEFAULT_RETAIL_STACKED_FIELD_STYLES,
  decoration: 'price_underline' as const,
  printOffsetMm: { vertical: 0, horizontal: 0 },
};

export const DEFAULT_ACTIVE_DESIGN_SEED_KEY = 'retail-stacked-default';
