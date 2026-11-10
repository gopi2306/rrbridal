import { BadRequestException, ServiceUnavailableException } from '@nestjs/common';
import {
  GstinApiAddressDetails,
  GstinApiTaxpayer,
  GstLookupResult,
  GstTaxpayerAddress,
  GstTaxpayerInfo,
} from './gst-lookup.types';

export const GSTIN_PATTERN = /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]$/i;

const STATE_BY_CODE: Record<string, string> = {
  '01': 'Jammu & Kashmir',
  '02': 'Himachal Pradesh',
  '03': 'Punjab',
  '04': 'Chandigarh',
  '05': 'Uttarakhand',
  '06': 'Haryana',
  '07': 'Delhi',
  '08': 'Rajasthan',
  '09': 'Uttar Pradesh',
  '10': 'Bihar',
  '11': 'Sikkim',
  '12': 'Arunachal Pradesh',
  '13': 'Nagaland',
  '14': 'Manipur',
  '15': 'Mizoram',
  '16': 'Tripura',
  '17': 'Meghalaya',
  '18': 'Assam',
  '19': 'West Bengal',
  '20': 'Jharkhand',
  '21': 'Odisha',
  '22': 'Chhattisgarh',
  '23': 'Madhya Pradesh',
  '24': 'Gujarat',
  '25': 'Daman & Diu',
  '26': 'Dadra & Nagar Haveli',
  '27': 'Maharashtra',
  '28': 'Andhra Pradesh',
  '29': 'Karnataka',
  '30': 'Goa',
  '31': 'Lakshadweep',
  '32': 'Kerala',
  '33': 'Tamil Nadu',
  '34': 'Puducherry',
  '35': 'Andaman & Nicobar Islands',
  '36': 'Telangana',
  '37': 'Andhra Pradesh',
  '38': 'Ladakh',
};

export const DEFAULT_GST_LOOKUP_BASE_URL = 'https://www.gstinapi.in/v1/gstin';

export function normalizeGstin(raw: string | undefined | null): string {
  return (raw ?? '').trim().toUpperCase();
}

export function isValidGstin(raw: string | undefined | null): boolean {
  return GSTIN_PATTERN.test(normalizeGstin(raw));
}

export function requireValidGstin(raw: string | undefined | null): string {
  const gstin = normalizeGstin(raw);
  if (!isValidGstin(gstin)) {
    throw new BadRequestException('Enter a valid 15-character GSTIN');
  }
  return gstin;
}

export function requireApiKey(key: string | undefined | null): string {
  const trimmed = (key ?? '').trim();
  if (!trimmed) {
    throw new ServiceUnavailableException('GST lookup is not configured on the server');
  }
  return trimmed;
}

export function resolveGstLookupBaseUrl(raw: string | undefined | null): string {
  const trimmed = (raw ?? '').trim().replace(/\/+$/, '');
  return trimmed || DEFAULT_GST_LOOKUP_BASE_URL;
}

export function buildGstLookupUrl(baseUrl: string, gstin: string): string {
  const base = resolveGstLookupBaseUrl(baseUrl);
  if (base.includes('{gstin}')) return base.replaceAll('{gstin}', encodeURIComponent(gstin));
  return `${base}/${encodeURIComponent(gstin)}`;
}

function text(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

function resolveState(stcd: string, gstin: string): string {
  const codeOrName = stcd.trim();
  if (/^\d{2}$/.test(codeOrName)) return STATE_BY_CODE[codeOrName] ?? codeOrName;
  if (codeOrName) return codeOrName;
  const fromGstin = gstin.slice(0, 2);
  return STATE_BY_CODE[fromGstin] ?? '';
}

function joinParts(...parts: string[]): string {
  return parts.map((p) => p.trim()).filter(Boolean).join(', ');
}

function unwrapTaxpayer(raw: unknown): GstTaxpayerInfo | null {
  if (!raw || typeof raw !== 'object') return null;
  const root = raw as Record<string, unknown>;
  if (root.flag === false) return null;

  const nested =
    (root.data && typeof root.data === 'object' ? (root.data as Record<string, unknown>) : null) ??
    (root.taxpayerInfo && typeof root.taxpayerInfo === 'object'
      ? (root.taxpayerInfo as Record<string, unknown>)
      : null);

  const candidate = (nested ?? root) as GstTaxpayerInfo;
  if (!candidate.lgnm && !candidate.tradeNam && !candidate.gstin) return null;
  return candidate;
}

function mapGstinApiPayload(raw: unknown, fallbackGstin: string): GstLookupResult | null {
  if (!raw || typeof raw !== 'object') return null;
  const root = raw as Record<string, unknown>;
  if (root.success === false) return null;

  const nested =
    root.data && typeof root.data === 'object' ? (root.data as GstinApiTaxpayer) : (root as GstinApiTaxpayer);
  const details: GstinApiAddressDetails = nested.address_details ?? {};
  const legalName = text(nested.legal_name);
  const tradeName = text(nested.trade_name);
  if (!legalName && !tradeName && !nested.address_details) return null;

  const gstin = normalizeGstin(nested.gstin || fallbackGstin);
  const doorNo = text(details.building_number);
  const street = joinParts(text(details.floor), text(details.building_name), text(details.street));
  const city =
    text(details.city) || text(details.locality) || text(nested.city) || text(details.district);
  const state = resolveState(text(details.state) || text(nested.state_code), gstin);
  const pincode = text(details.pincode) || text(nested.pincode);
  const fullAddress =
    text(nested.address) ||
    joinParts(doorNo, street, city, state, pincode);
  const status = text(nested.status);
  const name = tradeName || legalName;

  return {
    gstin,
    name,
    legalName,
    tradeName,
    status,
    isActive: !status || /^active$/i.test(status),
    doorNo,
    street,
    fullAddress,
    city,
    state,
    pincode,
  };
}

export function mapTaxpayerPayload(raw: unknown, fallbackGstin = ''): GstLookupResult | null {
  const fromGstinApi = mapGstinApiPayload(raw, fallbackGstin);
  if (fromGstinApi) return fromGstinApi;

  const info = unwrapTaxpayer(raw);
  if (!info) return null;

  const gstin = normalizeGstin(info.gstin || fallbackGstin);
  const legalName = text(info.lgnm);
  const tradeName = text(info.tradeNam);
  const name = tradeName || legalName;
  if (!name && !gstin) return null;

  const addr: GstTaxpayerAddress = info.pradr?.addr ?? {};
  const doorNo = text(addr.bno);
  const street = joinParts(text(addr.flno), text(addr.bnm), text(addr.st));
  const city = text(addr.loc) || text(addr.dst);
  const state = resolveState(text(addr.stcd), gstin);
  const pincode = text(addr.pncd);
  const fullAddress = joinParts(
    doorNo,
    street,
    city,
    text(addr.dst) && text(addr.dst) !== city ? text(addr.dst) : '',
    state,
    pincode,
  );
  const status = text(info.sts);
  const isActive = !status || /^active$/i.test(status);

  return {
    gstin,
    name,
    legalName,
    tradeName,
    status,
    isActive,
    doorNo,
    street,
    fullAddress,
    city,
    state,
    pincode,
  };
}
