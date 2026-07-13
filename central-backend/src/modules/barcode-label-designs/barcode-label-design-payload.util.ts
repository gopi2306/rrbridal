import {
  BARCODE_DECORATIONS,
  BARCODE_FIELD_STYLE_KEYS,
  type BarcodeFieldStyleKey,
  type BarcodeFontWeight,
} from './barcode-label-design.types';
import { DEFAULT_RETAIL_STACKED_FIELD_STYLES } from './barcode-label-design.seed';
import type { CreateBarcodeLabelDesignDto } from './dto/create-barcode-label-design.dto';
import type { UpdateBarcodeLabelDesignDto } from './dto/update-barcode-label-design.dto';

const PRINTER_PROFILE_ALIASES: Record<string, string> = {
  'printer-tsc-ttp-244': 'tsc-ttp-244-pro',
};

const FRONTEND_FIELD_STYLE_KEYS: Record<string, BarcodeFieldStyleKey> = {
  brand: 'brandName',
  itemName: 'productName',
  designNo: 'designSku',
  priceLabel: 'sellingPrice',
  priceValue: 'sellingPrice',
  code: 'barcodeNumber',
  sizeNote: 'sizeNote',
  batch: 'batchNumber',
  expiry: 'expiryDate',
};

type RawFieldStyle = {
  sizePt?: number;
  fontSizePt?: number;
  weight?: string;
  fontWeight?: number | string;
};

function normalizeFontWeight(weight: unknown): BarcodeFontWeight {
  if (typeof weight === 'string') {
    const normalized = weight.trim().toLowerCase();
    if (normalized === 'regular' || normalized === 'normal') return 'regular';
    if (normalized === 'bold') return 'bold';
  }
  if (typeof weight === 'number') {
    return weight >= 600 ? 'bold' : 'regular';
  }
  return 'bold';
}

function normalizeFieldStyle(raw: RawFieldStyle) {
  return {
    sizePt: raw.sizePt ?? raw.fontSizePt ?? 5.5,
    weight: normalizeFontWeight(raw.weight ?? raw.fontWeight),
  };
}

function extractStylesFromInput(input?: Record<string, unknown>) {
  const mapped: Partial<Record<BarcodeFieldStyleKey, { sizePt: number; weight: BarcodeFontWeight }>> =
    {};

  if (input) {
    for (const key of BARCODE_FIELD_STYLE_KEYS) {
      const raw = input[key];
      if (raw && typeof raw === 'object') {
        mapped[key] = normalizeFieldStyle(raw as RawFieldStyle);
      }
    }

    const frontend = input.__frontend;
    if (frontend && typeof frontend === 'object') {
      const fieldStyles = (frontend as { fieldStyles?: Record<string, RawFieldStyle> }).fieldStyles;
      if (fieldStyles && typeof fieldStyles === 'object') {
        for (const [frontendKey, raw] of Object.entries(fieldStyles)) {
          const backendKey = FRONTEND_FIELD_STYLE_KEYS[frontendKey];
          if (!backendKey || !raw || typeof raw !== 'object') continue;
          if (backendKey === 'sellingPrice' && frontendKey === 'priceLabel' && mapped.sellingPrice) {
            continue;
          }
          mapped[backendKey] = normalizeFieldStyle(raw);
        }
      }
    }
  }

  const frontend =
    input?.__frontend && typeof input.__frontend === 'object' ? input.__frontend : undefined;

  return { mapped, frontend };
}

export function resolveDecoration(
  decoration: string | undefined,
  styles?: Record<string, unknown>,
): string {
  if (decoration && decoration !== 'none') {
    return BARCODE_DECORATIONS.includes(decoration as (typeof BARCODE_DECORATIONS)[number])
      ? decoration
      : 'none';
  }

  const frontend = styles?.__frontend;
  if (frontend && typeof frontend === 'object') {
    const frontendDecoration = (
      frontend as {
        decoration?: { priceUnderline?: boolean; border?: string };
      }
    ).decoration;
    if (frontendDecoration?.priceUnderline) return 'price_underline';
    if (frontendDecoration?.border === 'square') return 'square_border';
    if (frontendDecoration?.border === 'rounded') return 'rounded_border';
  }

  return decoration ?? 'none';
}

export function normalizePrinterProfileId(profileId: string) {
  const trimmed = profileId.trim();
  return PRINTER_PROFILE_ALIASES[trimmed] ?? trimmed;
}

export function stylesRecordFromInput(input?: Record<string, unknown>) {
  const { mapped, frontend } = extractStylesFromInput(input);
  const record: Record<string, { sizePt: number; weight: BarcodeFontWeight } | unknown> = {};

  for (const key of BARCODE_FIELD_STYLE_KEYS) {
    const style = mapped[key] ?? DEFAULT_RETAIL_STACKED_FIELD_STYLES[key];
    record[key] = { sizePt: style.sizePt, weight: style.weight };
  }

  if (frontend !== undefined) {
    record.__frontend = frontend;
  }

  return record;
}

export function serializeStyles(styles: Record<string, unknown> | undefined) {
  const result: Record<string, unknown> = {};
  if (!styles) return result;

  for (const key of BARCODE_FIELD_STYLE_KEYS) {
    const value = styles[key];
    if (value && typeof value === 'object' && 'sizePt' in value) {
      result[key] = value;
    }
  }

  if (styles.__frontend) {
    result.__frontend = styles.__frontend;
  }

  return result;
}

function normalizeDesignDto<T extends CreateBarcodeLabelDesignDto | UpdateBarcodeLabelDesignDto>(
  dto: T,
): T {
  const styles = dto.styles as Record<string, unknown> | undefined;
  const normalized: Record<string, unknown> = { ...dto };

  if (dto.printerProfileId) {
    normalized.printerProfileId = normalizePrinterProfileId(dto.printerProfileId);
  }

  const hasFrontendDecoration =
    styles?.__frontend &&
    typeof styles.__frontend === 'object' &&
    typeof (styles.__frontend as { decoration?: unknown }).decoration === 'object';

  if (dto.decoration !== undefined || hasFrontendDecoration) {
    normalized.decoration = resolveDecoration(dto.decoration, styles);
  }

  if (styles) {
    normalized.styles = stylesRecordFromInput(styles) as T['styles'];
  }

  return normalized as T;
}

export function normalizeCreateBarcodeLabelDesignDto(dto: CreateBarcodeLabelDesignDto) {
  return normalizeDesignDto(dto);
}

export function normalizeUpdateBarcodeLabelDesignDto(dto: UpdateBarcodeLabelDesignDto) {
  return normalizeDesignDto(dto);
}
