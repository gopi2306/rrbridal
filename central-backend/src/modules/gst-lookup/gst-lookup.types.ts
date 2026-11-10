export type GstLookupResult = {
  gstin: string;
  name: string;
  legalName: string;
  tradeName: string;
  status: string;
  isActive: boolean;
  doorNo: string;
  street: string;
  fullAddress: string;
  city: string;
  state: string;
  pincode: string;
};

export type GstTaxpayerAddress = {
  bno?: string;
  flno?: string;
  bnm?: string;
  st?: string;
  loc?: string;
  dst?: string;
  stcd?: string;
  pncd?: string;
};

export type GstTaxpayerInfo = {
  gstin?: string;
  lgnm?: string;
  tradeNam?: string;
  sts?: string;
  pradr?: { addr?: GstTaxpayerAddress };
};

export type GstinApiAddressDetails = {
  building_number?: string | null;
  building_name?: string | null;
  floor?: string | null;
  street?: string | null;
  locality?: string | null;
  district?: string | null;
  city?: string | null;
  state?: string | null;
  landmark?: string | null;
  pincode?: string | null;
};

export type GstinApiTaxpayer = {
  gstin?: string;
  legal_name?: string | null;
  trade_name?: string | null;
  status?: string | null;
  state_code?: string | null;
  address?: string | null;
  city?: string | null;
  pincode?: string | null;
  address_details?: GstinApiAddressDetails | null;
};
