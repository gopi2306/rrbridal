import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';
import type { BillListPaymentModeKey, BillListStatusKey } from '../../dashboard/store-sales-payload.util';

const STATUSES = ['completed', 'partially_returned', 'returned', 'cancelled'] as const satisfies readonly BillListStatusKey[];
const PAYMENT_MODES = ['cash', 'card', 'upi', 'credit', 'mixed'] as const satisfies readonly BillListPaymentModeKey[];

export class BillsQueryDto {
  @ApiProperty({ required: false, description: 'Store code; defaults to first active store' })
  @IsString()
  @IsOptional()
  storeCode?: string;

  @ApiProperty({ required: false, description: 'Bill no, customer, SKU, or barcode' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false, description: 'IST start date YYYY-MM-DD; default last 30 days' })
  @IsString()
  @IsOptional()
  from?: string;

  @ApiProperty({ required: false, description: 'IST end date YYYY-MM-DD; default today' })
  @IsString()
  @IsOptional()
  to?: string;

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 20, minimum: 1, maximum: 100 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  @IsOptional()
  limit?: number;

  @ApiProperty({ required: false, enum: STATUSES })
  @IsIn(STATUSES)
  @IsOptional()
  status?: BillListStatusKey;

  @ApiProperty({ required: false, enum: PAYMENT_MODES })
  @IsIn(PAYMENT_MODES)
  @IsOptional()
  paymentMode?: BillListPaymentModeKey;

  @ApiProperty({ required: false, description: 'Filter by salesman code (or legacy salesman name substring)' })
  @IsString()
  @IsOptional()
  salesmanCode?: string;
}
