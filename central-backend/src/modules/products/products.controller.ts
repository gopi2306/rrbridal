import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';
import { ApiQuery, ApiTags } from '@nestjs/swagger';
import { CreateProductDto } from './dto/create-product.dto';
import { FilterProductDto } from './dto/filter-product.dto';
import { UpdateProductDto } from './dto/update-product.dto';
import { ProductsService } from './products.service';

@ApiTags('products')
@Controller('products')
export class ProductsController {
  constructor(private readonly productsService: ProductsService) {}

  @Post()
  async create(@Body() dto: CreateProductDto) {
    return await this.productsService.create(dto);
  }

  @Get()
  @ApiQuery({ name: 'search', required: false })
  @ApiQuery({ name: 'sku', required: false })
  @ApiQuery({ name: 'upcEanCode', required: false })
  @ApiQuery({ name: 'categoryId', required: false })
  @ApiQuery({ name: 'supplierNameId', required: false })
  async list(
    @Query('search') search?: string,
    @Query('sku') sku?: string,
    @Query('upcEanCode') upcEanCode?: string,
    @Query('categoryId') categoryId?: string,
    @Query('supplierNameId') supplierNameId?: string,
  ) {
    const params: { search?: string; sku?: string; upcEanCode?: string; categoryId?: string; supplierNameId?: string } = {};
    if (search) params.search = search;
    if (sku) params.sku = sku;
    if (upcEanCode) params.upcEanCode = upcEanCode;
    if (categoryId) params.categoryId = categoryId;
    if (supplierNameId) params.supplierNameId = supplierNameId;
    return await this.productsService.list(params);
  }

  @Post('filter')
  async filter(@Body() dto: FilterProductDto) {
    return await this.productsService.filter(dto);
  }

  @Get(':id')
  async get(@Param('id') id: string) {
    return await this.productsService.findById(id);
  }

  @Patch(':id')
  async update(@Param('id') id: string, @Body() dto: UpdateProductDto) {
    return await this.productsService.update(id, dto);
  }
}

