import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const statuses = ['draft', 'posted'] as const;

export class FilterGoodsReceiptDto {
  @ApiProperty({ required: false, description: 'Search across receipt number, PO number, invoice number, supplier name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  receiptNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  invoiceNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  supplierId?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

  @ApiProperty({ required: false, description: 'Invoice date from (inclusive)' })
  @IsString()
  @IsOptional()
  invoiceDateFrom?: string;

  @ApiProperty({ required: false, description: 'Invoice date to (inclusive)' })
  @IsString()
  @IsOptional()
  invoiceDateTo?: string;

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 20, minimum: 1, maximum: 500 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  @IsOptional()
  limit?: number;

  @ApiProperty({ required: false, default: 'updatedAt', description: 'Field to sort by' })
  @IsString()
  @IsOptional()
  sortBy?: string;

  @ApiProperty({ required: false, default: 'desc', enum: ['asc', 'desc'] })
  @IsString()
  @IsOptional()
  sortOrder?: 'asc' | 'desc';
}
