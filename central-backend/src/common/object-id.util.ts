import { Types } from 'mongoose';

export function isValidObjectIdString(value: unknown): value is string {
  if (typeof value !== 'string') return false;
  const trimmed = value.trim();
  return trimmed.length > 0 && Types.ObjectId.isValid(trimmed);
}

export function toObjectId(value: string): Types.ObjectId {
  return new Types.ObjectId(value.trim());
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
