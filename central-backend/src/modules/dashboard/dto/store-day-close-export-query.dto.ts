import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsOptional, IsString, Matches } from 'class-validator';
import {
  STORE_DAY_CLOSE_EXPORT_FORMATS,
  type StoreDayCloseExportFormat,
} from '../store-day-close-report.types';

export class StoreDayCloseExportQueryDto {
  @ApiProperty({ enum: STORE_DAY_CLOSE_EXPORT_FORMATS })
  @IsIn([...STORE_DAY_CLOSE_EXPORT_FORMATS])
  format!: StoreDayCloseExportFormat;

  @IsOptional()
  @IsString()
  storeId?: string;

  @IsOptional()
  @IsString()
  @Matches(/^\d{4}-\d{2}-\d{2}$/)
  businessDate?: string;

  @IsOptional()
  @IsString()
  posCounter?: string;
}
