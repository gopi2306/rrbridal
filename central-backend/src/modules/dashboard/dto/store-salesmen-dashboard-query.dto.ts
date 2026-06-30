import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const PERIODS = ['today', 'week', 'month', 'year', 'custom'] as const;

export class StoreSalesmenDashboardQueryDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  storeId?: string;

  @ApiProperty({ required: false, default: 'today', enum: PERIODS })
  @IsIn(PERIODS)
  @IsOptional()
  period?: (typeof PERIODS)[number];

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  from?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  to?: string;

  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsInt()
  @Min(2000)
  @Max(2100)
  @IsOptional()
  year?: number;

  @ApiProperty({ required: false })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(12)
  @IsOptional()
  month?: number;

  @ApiProperty({ required: false, default: 50, minimum: 1, maximum: 200 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(200)
  @IsOptional()
  invoiceLimit?: number;
}

export class StoreSalesmanDashboardQueryDto extends StoreSalesmenDashboardQueryDto {
  @ApiProperty({ description: 'Salesman Mongo _id or __legacy__ for bills without salesman code' })
  @IsString()
  salesmanId!: string;
}
