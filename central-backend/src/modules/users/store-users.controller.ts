import { Controller, Get, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { UsersService } from './users.service';

@ApiTags('store-users')
@Controller('store-users')
export class StoreUsersController {
  constructor(private readonly usersService: UsersService) {}

  @Get()
  @ApiQuery({ name: 'storeId', required: true })
  async listByStore(@Query('storeId') storeId: string) {
    return await this.usersService.listByStoreWithHash(storeId);
  }
}
