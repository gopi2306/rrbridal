import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import type { FilterQuery, Model } from 'mongoose';
import { roundMoney } from '../../common/money.util';
import { Branch, BranchDocument } from '../branches/schemas/branch.schema';
import { Division, DivisionDocument } from '../divisions/schemas/division.schema';
import { Location, LocationDocument } from '../locations/schemas/location.schema';
import {
  PurchaseReturn,
  PurchaseReturnDocument,
  PurchaseReturnLine,
} from '../purchase-returns/schemas/purchase-return.schema';
import { Product, ProductDocument } from '../products/schemas/product.schema';
import { PurchaseReturnReportQueryDto } from './dto/purchase-return-report-query.dto';
import {
  deriveIgstAmount,
  masterLookupStages,
  parsePurchaseReturnNoNumeric,
  readPopulatedCode,
  readPopulatedName,
} from './purchase-return-report.util';
import type {
  PurchaseReturnReportResponse,
  PurchaseReturnReportRow,
  PurchaseReturnReportTotals,
} from './purchase-return-report.types';

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function is24HexObjectId(s: string): boolean {
  return /^[a-fA-F0-9]{24}$/.test(s);
}

@Injectable()
export class PurchaseReturnReportService {
  constructor(
    @InjectModel(PurchaseReturn.name) private readonly prModel: Model<PurchaseReturnDocument>,
    @InjectModel(Branch.name) private readonly branchModel: Model<BranchDocument>,
    @InjectModel(Division.name) private readonly divisionModel: Model<DivisionDocument>,
    @InjectModel(Location.name) private readonly locationModel: Model<LocationDocument>,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  async buildReport(query: PurchaseReturnReportQueryDto): Promise<PurchaseReturnReportResponse> {
    const filter = await this.buildFilter(query);
    const limit = query.limit ?? 10000;

    const docs = await this.prModel.aggregate([
      { $match: filter as Record<string, unknown> },
      { $sort: { purchaseReturnDate: 1, purchaseReturnNo: 1 } },
      ...masterLookupStages(this.branchModel.collection.name, 'branchId', 'branch'),
      ...masterLookupStages(this.divisionModel.collection.name, 'mainDivisionId', 'mainDivision'),
      ...masterLookupStages(this.locationModel.collection.name, 'mainLocationId', 'mainLocation'),
    ]);

    const skus = new Set<string>();
    for (const doc of docs) {
      const lines = Array.isArray(doc.lines) ? (doc.lines as PurchaseReturnLine[]) : [];
      for (const line of lines) {
        if (line.sku?.trim()) skus.add(line.sku.trim());
      }
    }

    const aliasBySku = await this.loadAliasBySku([...skus]);

    const allRows: PurchaseReturnReportRow[] = [];
    for (const doc of docs) {
      const lines = Array.isArray(doc.lines) ? (doc.lines as PurchaseReturnLine[]) : [];
      if (lines.length === 0) continue;

      const purchaseReturnNo = String(doc.purchaseReturnNo ?? '');
      const purchaseReturnNoNumeric = parsePurchaseReturnNoNumeric(purchaseReturnNo);
      const supplierName = String(doc.supplier?.name ?? '');
      const companyName = readPopulatedName(doc.branch);
      const divisionName = readPopulatedName(doc.mainDivision);
      const locationName = readPopulatedName(doc.mainLocation);
      const retailOutletId = readPopulatedCode(doc.branch) || '1';
      const slipNo = String(doc.pucOutSlipNo ?? '');
      const date = String(doc.purchaseReturnDate ?? '');

      for (const line of lines) {
        const sku = line.sku?.trim() ?? '';
        const cgstAmount = roundMoney(num(line.cgstAmount));
        const sgstAmount = roundMoney(num(line.sgstAmount));
        const igstAmount = roundMoney(deriveIgstAmount(line));
        const totalAmount = roundMoney(num(line.netAmount, num(line.amount)));

        allRows.push({
          date,
          purchaseReturnNo,
          purchaseReturnNoNumeric,
          supplierName,
          itemName: String(line.description ?? ''),
          qty: num(line.recdQty),
          cgstAmount,
          sgstAmount,
          igstAmount,
          totalAmount,
          additionalDiscountPercent1: num(line.discountPercent),
          additionalDiscountPercent2: 0,
          additionalDiscountPercent3: 0,
          additionalDiscountAmount1: roundMoney(num(line.discountAmount)),
          additionalDiscountAmount2: roundMoney(num(line.cashDiscAmount)),
          additionalDiscountAmount3: 0,
          additionalEduCessPercent: 0,
          bagQty: 0,
          cessA: roundMoney(num(line.surchargeAmount)),
          companyName,
          divisionName,
          eduCessPercent: 0,
          exciseDutyPercent: 0,
          freeQty: num(line.freeQty),
          itemAlias: aliasBySku.get(sku) ?? line.barcode ?? '',
          itemCode: sku,
          loadUnloadCharge: 0,
          locationName,
          mrp: roundMoney(num(line.mrp)),
          observationAmount: 0,
          purchaseNo: '',
          purchaseReturnReferenceNo: purchaseReturnNoNumeric,
          retailOutletId,
          slipNo,
          travelExpense: 0,
        });
      }
    }

    const truncated = allRows.length > limit;
    const data = truncated ? allRows.slice(0, limit) : allRows;

    return {
      period: {
        from: query.from,
        to: query.to,
        ...(query.branchId?.trim() ? { branchId: query.branchId.trim() } : {}),
        ...(query.mainDivisionId?.trim() ? { mainDivisionId: query.mainDivisionId.trim() } : {}),
        ...(query.mainLocationId?.trim() ? { mainLocationId: query.mainLocationId.trim() } : {}),
        ...(query.supplierId?.trim() ? { supplierId: query.supplierId.trim() } : {}),
        ...(query.status?.trim() ? { status: query.status.trim() } : {}),
      },
      truncated,
      total: allRows.length,
      totals: this.summarizeRows(data),
      data,
    };
  }

  private summarizeRows(rows: PurchaseReturnReportRow[]): PurchaseReturnReportTotals {
    return rows.reduce<PurchaseReturnReportTotals>(
      (acc, row) => ({
        qty: roundMoney(acc.qty + row.qty),
        cgstAmount: roundMoney(acc.cgstAmount + row.cgstAmount),
        sgstAmount: roundMoney(acc.sgstAmount + row.sgstAmount),
        igstAmount: roundMoney(acc.igstAmount + row.igstAmount),
        totalAmount: roundMoney(acc.totalAmount + row.totalAmount),
        mrp: roundMoney(acc.mrp + row.mrp),
      }),
      { qty: 0, cgstAmount: 0, sgstAmount: 0, igstAmount: 0, totalAmount: 0, mrp: 0 },
    );
  }

  private async buildFilter(query: PurchaseReturnReportQueryDto): Promise<FilterQuery<PurchaseReturnDocument>> {
    if (query.from > query.to) {
      throw new BadRequestException('from must be on or before to');
    }

    const [branchId, mainDivisionId, mainLocationId] = await Promise.all([
      this.resolveCodeOrId(this.branchModel, query.branchId),
      this.resolveCodeOrId(this.divisionModel, query.mainDivisionId),
      this.resolveCodeOrId(this.locationModel, query.mainLocationId),
    ]);

    const filter: FilterQuery<PurchaseReturnDocument> = {
      purchaseReturnDate: { $gte: query.from, $lte: query.to },
    };

    if (branchId !== undefined) filter.branchId = branchId;
    if (mainDivisionId !== undefined) filter.mainDivisionId = mainDivisionId;
    if (mainLocationId !== undefined) filter.mainLocationId = mainLocationId;
    if (query.supplierId?.trim()) filter['supplier.supplierId'] = query.supplierId.trim();
    if (query.status?.trim()) filter.status = query.status.trim() as PurchaseReturnDocument['status'];

    return filter;
  }

  private async resolveCodeOrId(model: Model<any>, value?: string): Promise<string | undefined> {
    if (!value?.trim()) return undefined;
    const trimmed = value.trim();
    if (is24HexObjectId(trimmed)) return trimmed;
    const found = (await model.findOne({ code: trimmed.toLowerCase() }).select('_id').lean()) as { _id: unknown } | null;
    return found ? String(found._id) : trimmed;
  }

  private async loadAliasBySku(skus: string[]): Promise<Map<string, string>> {
    if (skus.length === 0) return new Map();
    const products = await this.productModel.find({ sku: { $in: skus } }).select('sku alias').lean();
    const map = new Map<string, string>();
    for (const p of products) {
      if (p.sku && p.alias) map.set(p.sku, p.alias);
    }
    return map;
  }
}
