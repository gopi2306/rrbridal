import type { PipelineStage } from 'mongoose';

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

export function readPopulatedName(ref: unknown): string {
  if (!ref || typeof ref !== 'object') return '';
  const name = (ref as { name?: unknown }).name;
  return typeof name === 'string' ? name : '';
}

export function readPopulatedCode(ref: unknown): string {
  if (!ref || typeof ref !== 'object') return '';
  const code = (ref as { code?: unknown }).code;
  return typeof code === 'string' ? code : '';
}

export function parsePurchaseReturnNoNumeric(purchaseReturnNo: string): number {
  const digits = purchaseReturnNo.replace(/\D/g, '');
  if (!digits) return 0;
  const n = Number(digits);
  return Number.isFinite(n) ? n : 0;
}

export function deriveIgstAmount(line: {
  taxAmount?: number;
  cgstAmount?: number;
  sgstAmount?: number;
}): number {
  const tax = num(line.taxAmount);
  const cgst = num(line.cgstAmount);
  const sgst = num(line.sgstAmount);
  return Math.max(0, tax - cgst - sgst);
}

export function formatLegacyReportDate(ymd: string): string {
  if (!ymd?.trim()) return '';
  const d = new Date(`${ymd.trim()}T00:00:00.000Z`);
  if (Number.isNaN(d.getTime())) return ymd;
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const day = String(d.getUTCDate()).padStart(2, '0');
  return `${day}-${months[d.getUTCMonth()]}-${d.getUTCFullYear()}`;
}

export function masterLookupStages(
  fromCollection: string,
  localField: string,
  asKey: string,
): PipelineStage[] {
  const tmp = `_lk_${asKey}`;
  return [
    {
      $lookup: {
        from: fromCollection,
        let: { ref: { $toString: { $ifNull: [`$${localField}`, ''] } } },
        pipeline: [
          {
            $match: {
              $expr: {
                $or: [
                  {
                    $and: [
                      { $ne: ['$$ref', ''] },
                      {
                        $eq: [
                          '$_id',
                          { $convert: { input: '$$ref', to: 'objectId', onError: null, onNull: null } },
                        ],
                      },
                    ],
                  },
                  {
                    $and: [{ $ne: ['$$ref', ''] }, { $eq: ['$code', { $toLower: '$$ref' }] }],
                  },
                ],
              },
            },
          },
          {
            $project: {
              _id: 1,
              code: 1,
              name: 1,
            },
          },
        ],
        as: tmp,
      },
    },
    {
      $addFields: {
        [asKey]: { $cond: [{ $gt: [{ $size: `$${tmp}` }, 0] }, { $arrayElemAt: [`$${tmp}`, 0] }, null] },
      },
    },
    { $project: { [tmp]: 0 } },
  ];
}
