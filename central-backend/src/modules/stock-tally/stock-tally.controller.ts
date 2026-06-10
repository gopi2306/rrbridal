import { Body, Controller, Get, Patch, Post, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { StockTallyQueryDto } from './dto/stock-tally-query.dto';
import { StockTallySaveDto } from './dto/stock-tally-save.dto';
import { StockTallyScanDto } from './dto/stock-tally-scan.dto';
import { StockTallyUpdateLineDto } from './dto/stock-tally-update-line.dto';
import { StockTallyService } from './stock-tally.service';

@ApiTags('stock-tally')
@Controller('stock-tally')
export class StockTallyController {
  constructor(private readonly stockTallyService: StockTallyService) {}

  @Get()
  async getSession(@Query() query: StockTallyQueryDto) {
    const params: { page: number; limit: number; search?: string } = {
      page: query.page ?? 1,
      limit: query.limit ?? 50,
    };
    if (query.search !== undefined && query.search !== '') params.search = query.search;
    return await this.stockTallyService.getSession(query.storeCode, params);
  }

  @Post('scan')
  async scan(@Body() dto: StockTallyScanDto) {
    return await this.stockTallyService.scan(dto.storeCode, dto.barcodeOrSku, dto.qtyDelta ?? 1);
  }

  @Patch('lines')
  async updateLine(@Body() dto: StockTallyUpdateLineDto) {
    return await this.stockTallyService.updateLine(dto.storeCode, dto.sku, dto.scannedQty);
  }

  @Post('save')
  async save(@Body() dto: StockTallySaveDto) {
    return await this.stockTallyService.save(dto.storeCode);
  }
}
