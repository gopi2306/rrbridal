import { BadRequestException, NotFoundException } from '@nestjs/common';
import { Model, Types } from 'mongoose';
import { ProductDocument } from '../modules/products/schemas/product.schema';
import {
  PRODUCT_REF_OBJECT_ID_FIELDS,
  isValidObjectIdString,
  stripInvalidObjectIdRefs,
  toObjectId,
} from './object-id.util';

/** Resolves productId from explicit id or sku lookup; validates sku match when id is provided. */
export async function resolveProductIdForSku(
  productModel: Model<ProductDocument>,
  sku: string,
  productId?: string,
): Promise<Types.ObjectId | undefined> {
  const trimmedSku = sku.trim();
  if (!trimmedSku) throw new BadRequestException('Each line requires a non-empty sku');

  let resolved = productId?.trim();
  if (resolved && !isValidObjectIdString(resolved)) {
    throw new BadRequestException(`Invalid productId '${resolved}'`);
  }

  if (!resolved) {
    const bySku = await productModel.findOne({ sku: trimmedSku }).select('_id sku').lean();
    if (bySku?._id) resolved = String(bySku._id);
  } else {
    const exists = await productModel.findById(toObjectId(resolved)).select('_id sku').lean();
    if (!exists) throw new NotFoundException(`Product not found for id '${resolved}'`);
    if (exists.sku !== trimmedSku) {
      throw new BadRequestException(
        `productId '${resolved}' does not match line sku '${trimmedSku}' (product sku is '${exists.sku}')`,
      );
    }
  }

  return resolved ? toObjectId(resolved) : undefined;
}

/** Attaches populated `product` on each line (API response only). */
export async function attachLineProducts<T extends { lines?: Array<Record<string, unknown>> }>(
  productModel: Model<ProductDocument>,
  docs: T[],
): Promise<T[]> {
  const productIds = new Set<string>();
  const skus = new Set<string>();
  for (const doc of docs) {
    for (const line of doc.lines ?? []) {
      const pid = line.productId;
      if (pid != null && String(pid).trim() !== '') {
        productIds.add(String(pid));
      } else if (typeof line.sku === 'string' && line.sku.trim()) {
        skus.add(line.sku.trim());
      }
    }
  }

  const byId = new Map<string, Record<string, unknown>>();
  if (productIds.size > 0) {
    const ids = [...productIds].filter((id) => isValidObjectIdString(id)).map((id) => toObjectId(id));
    const rows = await productModel.find({ _id: { $in: ids } }).lean();
    const cleaned = rows.map((row) =>
      stripInvalidObjectIdRefs({ ...row } as Record<string, unknown>, PRODUCT_REF_OBJECT_ID_FIELDS),
    );
    await productModel.populate(
      cleaned,
      PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
    );
    for (const row of cleaned) {
      if (row._id != null) byId.set(String(row._id), row);
    }
  }

  const bySku = new Map<string, Record<string, unknown>>();
  if (skus.size > 0) {
    const rows = await productModel.find({ sku: { $in: [...skus] } }).lean();
    const cleaned = rows.map((row) =>
      stripInvalidObjectIdRefs({ ...row } as Record<string, unknown>, PRODUCT_REF_OBJECT_ID_FIELDS),
    );
    await productModel.populate(
      cleaned,
      PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
    );
    for (const row of cleaned) {
      if (typeof row.sku === 'string') bySku.set(row.sku, row);
    }
  }

  return docs.map((doc) => ({
    ...doc,
    lines: (doc.lines ?? []).map((line) => {
      const pid = line.productId != null ? String(line.productId) : '';
      const sku = typeof line.sku === 'string' ? line.sku.trim() : '';
      const product =
        (pid && byId.get(pid)) ??
        (sku && bySku.get(sku)) ??
        null;
      return { ...line, product };
    }),
  }));
}

export async function enrichDocWithLineProducts<T extends Record<string, unknown>>(
  productModel: Model<ProductDocument>,
  doc: T,
): Promise<T> {
  const [enriched] = await attachLineProducts(productModel, [doc as { lines?: Array<Record<string, unknown>> }]);
  return enriched as T;
}

/** Populates master refs on product documents (API responses). */
export async function enrichProductDocuments(
  productModel: Model<ProductDocument>,
  docs: Array<Record<string, unknown>>,
): Promise<Array<Record<string, unknown>>> {
  if (docs.length === 0) return [];
  const cleaned = docs.map((row) =>
    stripInvalidObjectIdRefs({ ...row } as Record<string, unknown>, PRODUCT_REF_OBJECT_ID_FIELDS),
  );
  await productModel.populate(
    cleaned,
    PRODUCT_REF_OBJECT_ID_FIELDS.map((path) => ({ path })),
  );
  return cleaned;
}
