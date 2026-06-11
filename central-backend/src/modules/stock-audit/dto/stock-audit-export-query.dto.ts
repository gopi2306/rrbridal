import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsOptional, IsString } from 'class-validator';
import {
  INVENTORY_EXPORT_FORMATS,
  type InventoryExportFormat,
} from '../../inventory/dto/inventory-export-query.dto';

export class StockAuditExportQueryDto {
  @ApiProperty({ enum: INVENTORY_EXPORT_FORMATS, description: 'Export file format' })
  @IsIn([...INVENTORY_EXPORT_FORMATS])
  format!: InventoryExportFormat;

  @ApiProperty({ required: true, example: 'store-001' })
  @IsString()
  storeCode!: string;

  @ApiProperty({ required: false, description: 'Filter by SKU or product name' })
  @IsString()
  @IsOptional()
  search?: string;
}
