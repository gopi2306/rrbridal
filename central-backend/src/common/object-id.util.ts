import { BadRequestException } from '@nestjs/common';
import { FilterQuery, Types } from 'mongoose';

export function isValidObjectIdString(value: unknown): value is string {
  if (typeof value !== 'string') return false;
  const trimmed = value.trim();
  return trimmed.length > 0 && Types.ObjectId.isValid(trimmed);
}

export function toObjectId(value: string): Types.ObjectId {
  return new Types.ObjectId(value.trim());
}

/** Equality filter for ref fields stored as ObjectId or legacy hex string in MongoDB. */
export function objectIdRefEquals(value: string): { $in: [Types.ObjectId, string] } {
  const trimmed = value.trim();
  return { $in: [new Types.ObjectId(trimmed), trimmed] };
}

/** Normalizes ObjectId refs from query/body (hex string, ObjectId, or populated `{ _id }`). */
export function coerceObjectIdString(value: unknown): string | undefined {
  if (value === undefined || value === null) return undefined;
  if (value instanceof Types.ObjectId) return value.toHexString();
  if (typeof value === 'string') {
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : undefined;
  }
  if (typeof value === 'object') {
    const obj = value as Record<string, unknown>;
    if ('_id' in obj) return coerceObjectIdString(obj._id);
    if (typeof obj.$oid === 'string') return coerceObjectIdString(obj.$oid);
  }
  return undefined;
}

/** Applies an exact-match filter on a product ref field; rejects invalid non-empty ids. */
export function applyObjectIdRefFilter<T extends Record<string, unknown>>(
  filter: FilterQuery<T>,
  field: string,
  value: unknown,
  label?: string,
): void {
  const id = coerceObjectIdString(value);
  if (id === undefined) return;
  if (!isValidObjectIdString(id)) {
    throw new BadRequestException(
      `${label ?? field} must be a valid 24-character hex ObjectId`,
    );
  }
  (filter as Record<string, unknown>)[field] = objectIdRefEquals(id);
}

/** Drops empty / invalid ObjectId ref values so Mongoose populate does not query `_id: { $in: [''] }`. */
export function stripInvalidObjectIdRefs<T extends Record<string, unknown>>(
  doc: T,
  refFields: readonly string[],
): T {
  for (const field of refFields) {
    const v = doc[field];
    if (v === '' || v === null) {
      (doc as Record<string, unknown>)[field] = undefined;
      continue;
    }
    if (typeof v === 'string' && !isValidObjectIdString(v)) {
      (doc as Record<string, unknown>)[field] = undefined;
    }
  }
  return doc;
}

export const PRODUCT_REF_OBJECT_ID_FIELDS = [
  'manufacturerNameId',
  'supplierNameId',
  'departmentId',
  'categoryId',
  'subCategoryId',
  'brandId',
  'weightAndSizeId',
  'weightPerGmOrMlId',
  'offerGroupId',
  'productStatusId',
  'colourId',
  'colourTypeId',
  'hsnCodeId',
  'gstUomId',
  'uomSubId',
  'batchExpiryDetailId',
  'itemPrepStatusId',
  'packedConfirmationId',
  'poQtyPolicyId',
  'sellById',
  'batchSelectionId',
  'skuTypeId',
  'skuOrderGroupId',
  'indentTypeId',
] as const;
