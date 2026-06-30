import { Controller, Get, Param, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { BillsService } from './bills.service';
import { BillsDetailQueryDto } from './dto/bills-detail-query.dto';
import { BillsQueryDto } from './dto/bills-query.dto';

@ApiTags('bills')
@Controller('bills')
export class BillsController {
  constructor(private readonly billsService: BillsService) {}

  @Get()
  async list(@Query() query: BillsQueryDto) {
    return await this.billsService.listBills({
      storeCode: query.storeCode,
      search: query.search,
      from: query.from,
      to: query.to,
      page: query.page ?? 1,
      limit: query.limit ?? 20,
      status: query.status,
      paymentMode: query.paymentMode,
      salesmanCode: query.salesmanCode,
    });
  }

  @Get(':billNo')
  async detail(@Param('billNo') billNo: string, @Query() query: BillsDetailQueryDto) {
    return await this.billsService.getBillDetail(query.storeCode, billNo);
  }
}
