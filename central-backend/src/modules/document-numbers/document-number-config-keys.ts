export const DOCUMENT_NUMBER_CONFIG_KEYS = [
  'product_sku',
  'customer_code',
  'salesman_code',
  'purchase_order',
  'purchase_intent',
  'purchase_return',
  'goods_receipt_rcv',
  'goods_receipt_grn',
] as const;

export type DocumentNumberConfigKey = (typeof DOCUMENT_NUMBER_CONFIG_KEYS)[number];

export function isDocumentNumberConfigKey(value: string): value is DocumentNumberConfigKey {
  return (DOCUMENT_NUMBER_CONFIG_KEYS as readonly string[]).includes(value);
}

export type DocumentNumberConfigDefault = {
  configKey: DocumentNumberConfigKey;
  prefix: string;
  padLength: number;
  startFrom: number;
  label: string;
  description?: string;
};

export const DOCUMENT_NUMBER_CONFIG_DEFAULTS: DocumentNumberConfigDefault[] = [
  {
    configKey: 'product_sku',
    prefix: 'SKU-',
    padLength: 6,
    startFrom: 1,
    label: 'Product SKU',
    description: 'Auto-generated product SKU',
  },
  {
    configKey: 'customer_code',
    prefix: 'CUST-',
    padLength: 4,
    startFrom: 1,
    label: 'Customer code',
    description: 'Auto-generated customer code for POS and central',
  },
  {
    configKey: 'salesman_code',
    prefix: 'SM',
    padLength: 3,
    startFrom: 1,
    label: 'Salesman code',
    description: 'Auto-generated salesman code per store',
  },
  {
    configKey: 'purchase_order',
    prefix: 'PO-',
    padLength: 6,
    startFrom: 1,
    label: 'Purchase order',
    description: 'Purchase order number (poNo)',
  },
  {
    configKey: 'purchase_intent',
    prefix: 'PINV-',
    padLength: 6,
    startFrom: 1,
    label: 'Purchase intent',
    description: 'Purchase intent number (intentNo)',
  },
  {
    configKey: 'purchase_return',
    prefix: 'PR-',
    padLength: 6,
    startFrom: 1,
    label: 'Purchase return',
    description: 'Purchase return number (purchaseReturnNo)',
  },
  {
    configKey: 'goods_receipt_rcv',
    prefix: 'RCV-',
    padLength: 6,
    startFrom: 1,
    label: 'Goods receipt',
    description: 'Goods receipt number (receiptNo)',
  },
  {
    configKey: 'goods_receipt_grn',
    prefix: 'GRN-',
    padLength: 6,
    startFrom: 1,
    label: 'GRN',
    description: 'Goods receipt GRN number (grnNumber)',
  },
];
