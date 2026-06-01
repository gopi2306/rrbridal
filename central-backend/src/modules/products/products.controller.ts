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
  @ApiQuery({
    name: 'search',
    required: false,
    description: 'Text search across itemName, shortName, alias, sku, upcEanCode',
  })
  @ApiQuery({ name: 'sku', required: false, description: 'Exact SKU match (takes precedence over skuContains)' })
  @ApiQuery({ name: 'skuContains', required: false, description: 'Case-insensitive SKU substring match' })
  @ApiQuery({ name: 'upcEanCode', required: false })
  @ApiQuery({ name: 'categoryId', required: false, description: '24-char hex category ObjectId' })
  @ApiQuery({
    name: 'supplierNameId',
    required: false,
    description: '24-char hex supplier ObjectId — list products for that supplier',
  })
  async list(
    @Query('search') search?: string,
    @Query('sku') sku?: string,
    @Query('skuContains') skuContains?: string,
    @Query('upcEanCode') upcEanCode?: string,
    @Query('categoryId') categoryId?: string,
    @Query('supplierNameId') supplierNameId?: string,
  ) {
    const params: {
      search?: string;
      sku?: string;
      skuContains?: string;
      upcEanCode?: string;
      categoryId?: string;
      supplierNameId?: string;
    } = {};
    if (search) params.search = search;
    if (sku) params.sku = sku;
    if (skuContains) params.skuContains = skuContains;
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

