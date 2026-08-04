import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import * as XLSX from 'xlsx';
import { isValidObjectIdString } from '../../../common/object-id.util';
import { GoodsReceiptsService } from '../../goods-receipts/goods-receipts.service';
import { Product, ProductDocument } from '../../products/schemas/product.schema';
import { SuppliersService } from '../../suppliers/suppliers.service';
import { PurchaseOrdersService } from '../purchase-orders.service';
import type {
  PurchaseOrderImportError,
  PurchaseOrderImportLine,
  PurchaseOrderImportOptions,
  PurchaseOrderImportResult,
} from './purchase-order-import.types';

const SHEET_NAME = 'PurchaseOrder';
const HEADERS = ['sku', 'quantity'] as const;

function normalizeHeader(value: string): string {
  return value.trim().toLowerCase().replace(/[\s_-]+/g, '');
}

function cellString(value: unknown): string {
  if (value == null) return '';
  return String(value).trim();
}

function parseQty(value: unknown): number | null {
  if (value == null || value === '') return null;
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }
  const cleaned = String(value).trim().replace(/,/g, '');
  if (!cleaned) return null;
  const n = Number(cleaned);
  return Number.isFinite(n) ? n : null;
}

@Injectable()
export class PurchaseOrderImportService {
  constructor(
    private readonly poService: PurchaseOrdersService,
    private readonly grService: GoodsReceiptsService,
    private readonly suppliersService: SuppliersService,
    @InjectModel(Product.name) private readonly productModel: Model<ProductDocument>,
  ) {}

  buildExcelTemplate(): Buffer {
    const exampleRow = ['BSH-1084', 44];
    const sheet = XLSX.utils.aoa_to_sheet([[...HEADERS], exampleRow]);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, sheet, SHEET_NAME);
    return Buffer.from(XLSX.write(workbook, { type: 'buffer', bookType: 'xlsx' }));
  }

  async importPoOnly(
    buffer: Buffer,
    options: PurchaseOrderImportOptions,
  ): Promise<PurchaseOrderImportResult> {
    return await this.runImport(buffer, options, { receiveAndPost: false });
  }

  async importReceiveAndPost(
    buffer: Buffer,
    options: PurchaseOrderImportOptions,
  ): Promise<PurchaseOrderImportResult> {
    return await this.runImport(buffer, options, { receiveAndPost: true });
  }

  private async runImport(
    buffer: Buffer,
    options: PurchaseOrderImportOptions,
    mode: { receiveAndPost: boolean },
  ): Promise<PurchaseOrderImportResult> {
    const dryRun = options.dryRun === true;
    const supplierId = options.supplierId?.trim() ?? '';
    if (!supplierId) {
      throw new BadRequestException('supplierId is required');
    }
    if (!isValidObjectIdString(supplierId)) {
      throw new BadRequestException('supplierId must be a valid Mongo ObjectId');
    }

    const { totalRows, lines, errors: parseErrors } = this.parseWorkbook(buffer);
    const errors: PurchaseOrderImportError[] = [...parseErrors];
    const warnings: string[] = [];

    if (lines.length === 0 && errors.length === 0) {
      throw new BadRequestException('Excel has no data rows with sku and quantity');
    }

    let supplierName = options.supplierName?.trim() || undefined;
    let supplierOk = false;
    try {
      const supplier = await this.suppliersService.findById(supplierId);
      supplierOk = true;
      if (!supplierName && typeof (supplier as { name?: string }).name === 'string') {
        supplierName = (supplier as { name?: string }).name;
      }
    } catch (err) {
      if (err instanceof NotFoundException) {
        errors.push({ row: 0, message: `Supplier not found for id '${supplierId}'` });
      } else {
        throw err;
      }
    }

    const skus = [...new Set(lines.map((l) => l.sku))];
    const products =
      skus.length > 0
        ? await this.productModel.find({ sku: { $in: skus } }).select('_id sku').lean()
        : [];
    const productBySku = new Map(products.map((p) => [p.sku, p]));

    for (const line of lines) {
      if (!productBySku.has(line.sku)) {
        const message = `SKU ${line.sku}: product not found`;
        if (mode.receiveAndPost) {
          errors.push({ row: line.sourceRow, sku: line.sku, message });
        } else {
          warnings.push(message);
        }
      }
    }

    const result: PurchaseOrderImportResult = {
      dryRun,
      totalRows,
      lineCount: lines.length,
      warnings,
      errors,
    };

    if (errors.length > 0 || !supplierOk || lines.length === 0) {
      return result;
    }

    if (dryRun) {
      return result;
    }

    const supplierPayload: { supplierId: string; name?: string } = { supplierId };
    if (supplierName) supplierPayload.name = supplierName;

    const createDto: Parameters<PurchaseOrdersService['create']>[0] = {
      supplier: supplierPayload,
      lines: lines.map((l) => ({ sku: l.sku, recdQty: l.qty })),
      status: 'open',
    };
    if (options.branchId?.trim()) createDto.branchId = options.branchId.trim();
    if (options.mainDivisionId?.trim()) createDto.mainDivisionId = options.mainDivisionId.trim();
    if (options.mainLocationId?.trim()) createDto.mainLocationId = options.mainLocationId.trim();

    const created = await this.poService.create(createDto);
    const poId = String((created as { _id?: unknown })._id ?? '');
    const poNo = typeof (created as { poNo?: string }).poNo === 'string' ? (created as { poNo: string }).poNo : '';

    result.poId = poId;
    result.poNo = poNo;

    const refreshed = await this.poService.refresh(poId);
    const refreshWarnings = (refreshed as { refreshWarnings?: string[] }).refreshWarnings;
    if (Array.isArray(refreshWarnings)) {
      for (const w of refreshWarnings) {
        if (!warnings.includes(w)) warnings.push(w);
      }
    }

    if (!mode.receiveAndPost) {
      return result;
    }

    const refreshedLines =
      ((refreshed as { lines?: Array<{ sku?: string; recdQty?: number; productId?: string; description?: string }> })
        .lines ?? []) as Array<{
        sku?: string;
        recdQty?: number;
        productId?: string;
        description?: string;
      }>;

    const gr = await this.grService.create({
      poId,
      poNo,
      supplier: supplierPayload,
      status: 'draft',
      lines: refreshedLines.map((line) => {
        const sku = (line.sku ?? '').trim();
        const qty = Math.max(0, line.recdQty ?? 0);
        const grLine: {
          sku: string;
          orderedQty: number;
          receivedQty: number;
          outcome: 'valid';
          productId?: string;
          description?: string;
        } = {
          sku,
          orderedQty: qty,
          receivedQty: qty,
          outcome: 'valid',
        };
        if (line.productId) grLine.productId = String(line.productId);
        if (line.description) grLine.description = line.description;
        return grLine;
      }),
    });

    const grId = String((gr as { _id?: unknown })._id ?? '');
    const receiptNo =
      typeof (gr as { receiptNo?: string }).receiptNo === 'string'
        ? (gr as { receiptNo: string }).receiptNo
        : undefined;

    await this.grService.postToInventory(grId);
    await this.poService.setStatus(poId, 'received');

    result.grId = grId;
    if (receiptNo) result.receiptNo = receiptNo;
    result.posted = true;
    return result;
  }

  private parseWorkbook(buffer: Buffer): {
    totalRows: number;
    lines: PurchaseOrderImportLine[];
    errors: PurchaseOrderImportError[];
  } {
    const workbook = XLSX.read(buffer, { type: 'buffer', raw: false });
    const sheet = workbook.Sheets[SHEET_NAME] ?? workbook.Sheets[workbook.SheetNames[0] ?? ''];
    if (!sheet) throw new BadRequestException('Excel file has no sheets');

    const rows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: '' }) as unknown[][];
    if (rows.length < 2) {
      throw new BadRequestException('Excel must have a header row and at least one data row');
    }

    const headers = (rows[0] ?? []).map((c) => normalizeHeader(String(c ?? '')));
    const skuIdx = headers.findIndex((h) => h === 'sku');
    const qtyIdx = headers.findIndex((h) => h === 'quantity' || h === 'qty');

    if (skuIdx < 0) {
      throw new BadRequestException('Excel must include a sku column');
    }
    if (qtyIdx < 0) {
      throw new BadRequestException('Excel must include a quantity (or qty) column');
    }

    const errors: PurchaseOrderImportError[] = [];
    const bySku = new Map<string, PurchaseOrderImportLine>();
    let totalRows = 0;

    for (let i = 1; i < rows.length; i++) {
      const row = rows[i] ?? [];
      const excelRow = i + 1;
      const sku = cellString(row[skuIdx]);
      const qtyRaw = row[qtyIdx];
      const qtyEmpty = qtyRaw == null || qtyRaw === '';

      if (!sku && qtyEmpty) continue;

      totalRows++;

      if (!sku) {
        errors.push({ row: excelRow, message: 'sku is required' });
        continue;
      }

      const qty = parseQty(qtyRaw);
      if (qty == null) {
        errors.push({ row: excelRow, sku, message: 'quantity must be a number' });
        continue;
      }
      if (!(qty > 0)) {
        errors.push({ row: excelRow, sku, message: 'quantity must be greater than 0' });
        continue;
      }

      const existing = bySku.get(sku);
      if (existing) {
        existing.qty += qty;
      } else {
        bySku.set(sku, { sku, qty, sourceRow: excelRow });
      }
    }

    return { totalRows, lines: [...bySku.values()], errors };
  }
}
