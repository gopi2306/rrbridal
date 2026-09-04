import { Injectable } from '@nestjs/common';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { InventoryService } from '../inventory/inventory.service';
import { SkuSalesReportQueryDto } from './dto/sku-sales-report-query.dto';
import { SkuSalesReportLoader } from './sku-sales-report.loader';
import { FAST_SELLERS_DEFAULT_LIMIT, type FastSellersReportResponse } from './sku-sales-report.types';
import {
  attachStockLevels,
  filterSkuRowsForFastSearch,
  rankFastSellers,
  totalsFromSkuRows,
} from './sku-sales-report.util';

@Injectable()
export class FastSellersReportService {
  constructor(
    private readonly loader: SkuSalesReportLoader,
    private readonly inventoryService: InventoryService,
  ) {}

  async buildReport(query: SkuSalesReportQueryDto): Promise<FastSellersReportResponse> {
    const loaded = await this.loader.loadSkuSales({
      ...query,
      limit: TABULAR_EXPORT_MAX_ROWS,
    });
    const matchQty =
      typeof query.matchQty === 'number' && Number.isFinite(query.matchQty)
        ? Math.max(0, query.matchQty)
        : 0;
    const storeQtyMap = await this.inventoryService.getStoreSkuQtyMap(loaded.period.storeCode);
    const filtered = filterSkuRowsForFastSearch(loaded.rows, loaded.search ?? '');
    const ranked = rankFastSellers(filtered);
    const limit = query.limit ?? FAST_SELLERS_DEFAULT_LIMIT;
    const page = ranked.slice(0, limit);
    const withStock = attachStockLevels(page, storeQtyMap, matchQty);
    const data = withStock.map((row, index) => ({ ...row, rank: index + 1 }));
    return {
      period: loaded.period,
      filters: { ...(loaded.search ? { search: loaded.search } : {}) },
      limit,
      truncated: loaded.invoiceTruncated || ranked.length > limit,
      total: ranked.length,
      totals: totalsFromSkuRows(data),
      data,
    };
  }
}
