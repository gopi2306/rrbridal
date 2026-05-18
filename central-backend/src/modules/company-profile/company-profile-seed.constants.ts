import { COMPANY_PROFILE_KEY } from './schemas/company-profile.schema';

/** Default company + thermal receipt template (Red Rose Mart reference layout). */
export const SEED_COMPANY_PROFILE = {
  settingsKey: COMPANY_PROFILE_KEY,
  legalName: 'RED ROSE MART',
  tradeName: 'RED ROSE MART',
  gstin: '36ABFFR4340C1ZI',
  address:
    '18-8-223/120/8,B/A,E,F/1, Khaliq plaza, Ghouse Nagar Colony, Kanchanbagh, Hyderabad, Hyd - 500058',
  city: 'Hyderabad',
  state: 'Telangana',
  pinCode: '500058',
  phone: '+91 90308 03762',
  email: 'care@redrosemart.com',
  companyLogo: 'https://redrosemart.com/cdn/shop/files/logo_redrosemart.png?height=628&pad_color=ffffff&v=1703753007&width=1200',
  fssaiNo: '13623015000605',
  website: 'www.redrosemart.com',
  termsAndConditions:
    'Goods once sold are subject to manufacturer warranty and store exchange policy. Please verify items and bill before leaving the counter.',
  thankYouLine: 'Thank For shopping In Red Rose Mart!! Visit again!!',
  policyLines: [
    'Offer Items Not Refundable',
    'NO EXCHANGE NO RETURN ON FROZEN & DAIRY PRODUCTS',
    'Return & Exchange Timing 12PM to 9PM',
  ],
  receiptQrSlots: [
    {
      label: 'KANCHANBAGH LOCATION',
      payload: 'https://maps.google.com/?q=Red+Rose+Mart+Kanchanbagh+Hyderabad',
    },
    {
      label: 'RED ROSE MART INSTAGRAM',
      payload: 'https://www.instagram.com/redrosemart',
    },
    {
      label: 'WIIZ FASHION INSTAGRAM',
      payload: 'https://www.instagram.com/wiizfashion',
    },
  ],
  receiptBarcodeEnabled: true,
  extraFields: {
    branchCode: 'RRM02',
  },
} as const;

/** Per-store thermal printer defaults (3-inch / 80mm). */
export const SEED_STORE_RECEIPT_PRINT_SETTINGS = {
  printerModel: 'TVS RP 3200 Lite',
  billPrinterQueueName: 'TVS RP 3200',
  receiptCharWidth: 48,
  alwaysUsePrintDialog: false,
  paperWidthMm: 80,
} as const;
