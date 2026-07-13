export const BARCODE_LAYOUT_STYLES = ['brand_price', 'retail_stacked'] as const;
export type BarcodeLayoutStyle = (typeof BARCODE_LAYOUT_STYLES)[number];

export const BARCODE_PRODUCT_NAME_SOURCES = ['itemName', 'shortName', 'alias'] as const;
export type BarcodeProductNameSource = (typeof BARCODE_PRODUCT_NAME_SOURCES)[number];

export const BARCODE_PRICE_STYLES = ['whole', 'decimal'] as const;
export type BarcodePriceStyle = (typeof BARCODE_PRICE_STYLES)[number];

export const BARCODE_HUMAN_TEXT_STYLES = ['sku_spaced', 'raw'] as const;
export type BarcodeHumanTextStyle = (typeof BARCODE_HUMAN_TEXT_STYLES)[number];

export const BARCODE_TEXT_ALIGNMENTS = ['left', 'center', 'right'] as const;
export type BarcodeTextAlignment = (typeof BARCODE_TEXT_ALIGNMENTS)[number];

export const BARCODE_DECORATIONS = [
  'none',
  'square_border',
  'rounded_border',
  'price_underline',
] as const;
export type BarcodeDecoration = (typeof BARCODE_DECORATIONS)[number];

export const BARCODE_FIELD_STYLE_KEYS = [
  'productName',
  'designSku',
  'sellingPrice',
  'sizeNote',
  'batchNumber',
  'expiryDate',
  'brandName',
  'barcodeNumber',
] as const;
export type BarcodeFieldStyleKey = (typeof BARCODE_FIELD_STYLE_KEYS)[number];

export const BARCODE_FONT_WEIGHTS = ['regular', 'bold'] as const;
export type BarcodeFontWeight = (typeof BARCODE_FONT_WEIGHTS)[number];

export type BarcodeLabelFields = {
  productName: boolean;
  designSku: boolean;
  sellingPrice: boolean;
  sizeNote: boolean;
  batchNumber: boolean;
  expiryDate: boolean;
  brandName: boolean;
};

export type BarcodeLabelTextSettings = {
  productNameSource: BarcodeProductNameSource;
  designNoPrefix: string;
  pricePrefix: string;
  notePrefix: string;
  priceStyle: BarcodePriceStyle;
  barcodeHumanText: BarcodeHumanTextStyle;
  alignment: BarcodeTextAlignment;
};

export type BarcodeLabelBarcodeSettings = {
  heightMm: number;
  widthMm: number;
};

export type BarcodeLabelFieldStyle = {
  sizePt: number;
  weight: BarcodeFontWeight;
};

export type BarcodeLabelPrintOffsetMm = {
  vertical: number;
  horizontal: number;
};
