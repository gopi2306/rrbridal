import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsOptional, IsString } from 'class-validator';

export const INVENTORY_EXPORT_FORMATS = ['xlsx', 'csv', 'pdf'] as const;
export type InventoryExportFormat = (typeof INVENTORY_EXPORT_FORMATS)[number];

export class InventoryExportQueryDto {
  @ApiProperty({ enum: INVENTORY_EXPORT_FORMATS, description: 'Export file format' })
  @IsIn([...INVENTORY_EXPORT_FORMATS])
  format!: InventoryExportFormat;

  @ApiProperty({ required: false, description: 'Filter by SKU, barcode, or product name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({
    required: false,
    description: 'When set, store qty column is for this store only; included in filename and PDF title',
  })
  @IsString()
  @IsOptional()
  storeId?: string;
}
