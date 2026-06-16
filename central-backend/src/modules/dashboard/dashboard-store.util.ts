import { NotFoundException } from '@nestjs/common';
import { Model, Types } from 'mongoose';
import type { StoreDocument } from '../stores/schemas/store.schema';

/** Resolves store by `code` (e.g. store-001) or Mongo `_id`. */
export async function resolveDashboardStore(
  storeModel: Model<StoreDocument>,
  storeId?: string,
): Promise<{ code: string; name: string }> {
  const trimmed = storeId?.trim();

  if (trimmed) {
    const or: Array<Record<string, unknown>> = [{ code: trimmed.toLowerCase() }];
    if (Types.ObjectId.isValid(trimmed)) {
      or.unshift({ _id: new Types.ObjectId(trimmed) });
    }

    const store = await storeModel.findOne({ $or: or, status: 'active' }).lean();
    if (store) {
      return { code: store.code, name: store.name };
    }

    throw new NotFoundException(`Store '${trimmed}' not found or inactive`);
  }

  const store = await storeModel.findOne({ status: 'active' }).sort({ code: 1 }).lean();
  if (!store) {
    throw new NotFoundException('No active stores configured');
  }

  return { code: store.code, name: store.name };
}
