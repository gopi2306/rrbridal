import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Matches, Min } from 'class-validator';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export class PurchaseReturnReportQueryDto {
  @ApiProperty({ description: 'From date inclusive (YYYY-MM-DD)', example: '2026-06-01' })
  @IsString()
  @Matches(ISO_DATE, { message: 'from must be YYYY-MM-DD' })
  from!: string;

  @ApiProperty({ description: 'To date inclusive (YYYY-MM-DD)', example: '2026-06-30' })
  @IsString()
  @Matches(ISO_DATE, { message: 'to must be YYYY-MM-DD' })
  to!: string;

  @ApiProperty({ required: false, description: 'Branch id or code filter' })
  @IsString()
  @IsOptional()
  branchId?: string;

  @ApiProperty({ required: false, description: 'Division id or code filter' })
  @IsString()
  @IsOptional()
  mainDivisionId?: string;

  @ApiProperty({ required: false, description: 'Location id or code filter' })
  @IsString()
  @IsOptional()
  mainLocationId?: string;

  @ApiProperty({ required: false, description: 'Supplier ObjectId filter' })
  @IsString()
  @IsOptional()
  supplierId?: string;

  @ApiProperty({ required: false, enum: ['open', 'posted', 'closed'] })
  @IsString()
  @IsOptional()
  status?: string;

  @ApiProperty({ required: false, default: 10000, minimum: 1, description: 'Max line rows returned/exported' })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  limit?: number;
}
