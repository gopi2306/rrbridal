import { Body, Controller, Get, Param, Post, Query } from '@nestjs/common';
import { ApiOperation, ApiTags } from '@nestjs/swagger';
import { StorefrontCatalogQueryDto, StorefrontEnquiryDto } from './storefront.dto';
import { StorefrontService } from './storefront.service';

@ApiTags('storefront')
@Controller('storefront')
export class StorefrontController {
  constructor(private readonly service: StorefrontService) {}

  @Get('products')
  @ApiOperation({ summary: 'List active B2B product offers across all configured businesses' })
  products(@Query() query: StorefrontCatalogQueryDto) {
    return this.service.catalog(query);
  }

  @Get('categories')
  categories() {
    return this.service.categories();
  }

  @Get('products/:databaseKey/:productIdOrSku')
  product(
    @Param('databaseKey') databaseKey: string,
    @Param('productIdOrSku') productIdOrSku: string,
  ) {
    return this.service.detail(databaseKey, productIdOrSku);
  }

  @Post('enquiries')
  @ApiOperation({ summary: 'Split and record a retailer enquiry in each product source database' })
  enquiry(@Body() dto: StorefrontEnquiryDto) {
    return this.service.submitEnquiry(dto);
  }
}
