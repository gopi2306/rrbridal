import { Injectable } from '@nestjs/common';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { SkuSalesReportQueryDto } from './dto/sku-sales-report-query.dto';
import { SkuSalesReportLoader } from './sku-sales-report.loader';
import type { SupplierWiseReportResponse } from './sku-sales-report.types';
import {
  filterSkuRowsForSupplierSearch,
  groupSupplierWise,
  totalsFromSkuRows,
} from './sku-sales-report.util';

@Injectable()
export class SupplierWiseSalesReportService {
  constructor(private readonly loader: SkuSalesReportLoader) {}

  async buildReport(query: SkuSalesReportQueryDto): Promise<SupplierWiseReportResponse> {
    const loaded = await this.loader.loadSkuSales({
      ...query,
      limit: query.limit ?? TABULAR_EXPORT_MAX_ROWS,
    });
    const filtered = filterSkuRowsForSupplierSearch(loaded.rows, loaded.search ?? '');
    const grouped = groupSupplierWise(filtered);
    const skuTotals = totalsFromSkuRows(filtered);
    return {
      period: loaded.period,
      filters: { ...(loaded.search ? { search: loaded.search } : {}) },
      limit: loaded.invoiceLimit,
      truncated: loaded.invoiceTruncated,
      total: loaded.invoiceTotal,
      totals: {
        ...skuTotals,
        supplierCount: grouped.data.length,
      },
      data: grouped.data,
    };
  }
}
