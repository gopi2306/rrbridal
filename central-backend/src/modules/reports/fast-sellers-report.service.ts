import { Injectable } from '@nestjs/common';
import { TABULAR_EXPORT_MAX_ROWS } from '../../common/tabular-export';
import { SkuSalesReportQueryDto } from './dto/sku-sales-report-query.dto';
import { SkuSalesReportLoader } from './sku-sales-report.loader';
import { FAST_SELLERS_DEFAULT_LIMIT, type FastSellersReportResponse } from './sku-sales-report.types';
import {
  filterSkuRowsForFastSearch,
  rankFastSellers,
  totalsFromSkuRows,
} from './sku-sales-report.util';

@Injectable()
export class FastSellersReportService {
  constructor(private readonly loader: SkuSalesReportLoader) {}

  async buildReport(query: SkuSalesReportQueryDto): Promise<FastSellersReportResponse> {
    const loaded = await this.loader.loadSkuSales({
      ...query,
      limit: TABULAR_EXPORT_MAX_ROWS,
    });
    const filtered = filterSkuRowsForFastSearch(loaded.rows, loaded.search ?? '');
    const ranked = rankFastSellers(filtered);
    const limit = query.limit ?? FAST_SELLERS_DEFAULT_LIMIT;
    const data = ranked.slice(0, limit).map((row, index) => ({ ...row, rank: index + 1 }));
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
