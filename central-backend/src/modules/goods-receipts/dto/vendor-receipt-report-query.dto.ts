import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsOptional, IsString, Matches } from 'class-validator';

const statuses = ['draft', 'posted'] as const;

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export class VendorReceiptReportQueryDto {
  @ApiProperty({
    required: false,
    description: 'Goods receipt created date from (inclusive), ISO YYYY-MM-DD',
    example: '2026-01-01',
  })
  @IsString()
  @IsOptional()
  @Matches(ISO_DATE, { message: 'receiptDateFrom must be YYYY-MM-DD' })
  receiptDateFrom?: string;

  @ApiProperty({
    required: false,
    description: 'Goods receipt created date to (inclusive end of day), ISO YYYY-MM-DD',
    example: '2026-06-30',
  })
  @IsString()
  @IsOptional()
  @Matches(ISO_DATE, { message: 'receiptDateTo must be YYYY-MM-DD' })
  receiptDateTo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  supplierId?: string;

  @ApiProperty({ required: false, enum: statuses, default: 'posted' })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];
}
