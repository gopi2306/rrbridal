import { Controller, Get, Query } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { MyStoreQueryDto } from './dto/my-store-query.dto';
import { MyStoreService } from './my-store.service';
import type { MyStoreQueryLimits } from './my-store.types';

@ApiTags('my-store')
@Controller('my-store')
export class MyStoreController {
  constructor(private readonly myStoreService: MyStoreService) {}

  @Get()
  async getWorkspace(@Query() query: MyStoreQueryDto) {
    const limits: MyStoreQueryLimits = {
      purchaseIndentLimit: query.purchaseIndentLimit ?? 10,
      transferInLimit: query.transferInLimit ?? 10,
      transferOutLimit: query.transferOutLimit ?? 10,
      inventoryPreviewLimit: query.inventoryPreviewLimit ?? 20,
    };
    return await this.myStoreService.getWorkspace(query.storeId, limits);
  }
}
