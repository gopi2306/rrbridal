import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsOptional, IsString } from 'class-validator';

const SCOPES = ['store', 'warehouse'] as const;

export type SupplierWiseReportScope = (typeof SCOPES)[number];

export class SupplierWiseReportQueryDto {
  @ApiProperty({ enum: SCOPES, description: 'Stock location: store on-hand or warehouse on-hand' })
  @IsIn(SCOPES)
  scope!: SupplierWiseReportScope;

  @ApiProperty({
    required: false,
    description: 'Store code; omit for all stores (store scope stock summed across stores)',
  })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ required: false, description: 'Supplier name or product/SKU contains' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, description: 'Brand Mongo id or code' })
  @IsString()
  @IsOptional()
  brandId?: string;

  @ApiProperty({ required: false, description: 'Category Mongo id or code' })
  @IsString()
  @IsOptional()
  categoryId?: string;

  @ApiProperty({
    required: false,
    description: 'Narrow supplier list (supplier overview endpoint only)',
  })
  @IsString()
  @IsOptional()
  supplierId?: string;
}
