import { BadRequestException } from '@nestjs/common';
import { formatBusinessYmd } from '../store-sales-payload.util';
import type { StoreDayCloseDashboardOptions } from '../store-day-close-dashboard.types';

const YMD = /^\d{4}-\d{2}-\d{2}$/;

export type StoreDayCloseQueryInput = {
  storeId?: string;
  /** Preferred query param for business calendar day */
  date?: string;
  /** Alias of `date` (backward compatible) */
  businessDate?: string;
  posCounter?: string;
};

export function resolveStoreDayCloseBusinessDate(query: StoreDayCloseQueryInput): string {
  const fromDate = query.date?.trim();
  if (fromDate) return fromDate;

  const fromBusinessDate = query.businessDate?.trim();
  if (fromBusinessDate) return fromBusinessDate;

  return formatBusinessYmd(new Date());
}

export function resolveStoreDayCloseOptions(
  query: StoreDayCloseQueryInput,
): StoreDayCloseDashboardOptions {
  const businessDate = resolveStoreDayCloseBusinessDate(query);
  if (!YMD.test(businessDate)) {
    throw new BadRequestException('date must be YYYY-MM-DD');
  }

  const options: StoreDayCloseDashboardOptions = { businessDate };
  const storeId = query.storeId?.trim();
  if (storeId) options.storeId = storeId;

  const posCounter = query.posCounter?.trim();
  if (posCounter) options.posCounter = posCounter;

  return options;
}
