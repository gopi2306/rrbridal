import { Body, Controller, Get, Param, Patch, Post, Query } from '@nestjs/common';

import { ApiQuery, ApiTags } from '@nestjs/swagger';

import { CreateProductDto } from './dto/create-product.dto';

import { FilterProductDto } from './dto/filter-product.dto';

import { ListProductsQueryDto } from './dto/list-products-query.dto';

import { UpdateProductDto } from './dto/update-product.dto';

import { ProductsService, ProductListFilterParams } from './products.service';



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

  @ApiQuery({ name: 'supplierId', required: false, description: 'Alias for supplierNameId' })

  async list(@Query() query: ListProductsQueryDto) {

    return await this.productsService.list(this.toListParams(query));

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



  private toListParams(query: ListProductsQueryDto): ProductListFilterParams & {

    skip?: number;

    limit?: number;

  } {

    const params: ProductListFilterParams & { skip?: number; limit?: number } = {};
    if (query.search) params.search = query.search;
    if (query.sku) params.sku = query.sku;
    if (query.skuContains) params.skuContains = query.skuContains;
    if (query.upcEanCode) params.upcEanCode = query.upcEanCode;
    if (query.categoryId) params.categoryId = query.categoryId;
    const supplierNameId = query.supplierNameId || query.supplierId;
    if (supplierNameId) params.supplierNameId = supplierNameId;
    if (query.skip !== undefined) params.skip = query.skip;
    if (query.limit !== undefined) params.limit = query.limit;
    return params;
  }

}



