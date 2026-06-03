import { ApiProperty } from '@nestjs/swagger';
import { Transform, Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

function trimOptional({ value }: { value: unknown }): unknown {
  return typeof value === 'string' ? value.trim() : value;
}

export class ListProductsQueryDto {
  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Transform(trimOptional)
  search?: string;

  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Transform(trimOptional)
  sku?: string;

  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Transform(trimOptional)
  skuContains?: string;

  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Transform(trimOptional)
  upcEanCode?: string;

  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @Transform(trimOptional)
  categoryId?: string;

  @ApiProperty({ required: false, description: 'Supplier ObjectId (24-char hex)' })
  @IsOptional()
  @IsString()
  @Transform(trimOptional)
  supplierNameId?: string;

  @ApiProperty({ required: false, description: 'Alias for supplierNameId' })
  @IsOptional()
  @IsString()
  @Transform(trimOptional)
  supplierId?: string;

  @ApiProperty({ required: false, default: 0 })
  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(0)
  skip?: number;

  @ApiProperty({ required: false, default: 200, maximum: 500 })
  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  limit?: number;
}
