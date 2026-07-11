import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsIn,
  IsNotEmpty,
  IsOptional,
  IsString,
  MaxLength,
  ValidateIf,
  ValidateNested,
} from 'class-validator';
import { InventoryAdjustmentLineDto } from './inventory-adjustment-line.dto';

export class CreateInventoryAdjustmentDto {
  @ApiProperty({ enum: ['store', 'warehouse'] })
  @IsIn(['store', 'warehouse'])
  locationKind!: 'store' | 'warehouse';

  @ApiProperty({ required: false, example: 'store-001' })
  @ValidateIf((dto: CreateInventoryAdjustmentDto) => dto.locationKind === 'store')
  @IsString()
  @IsNotEmpty()
  storeCode?: string;

  @ApiProperty({ required: false, example: 'wh-main' })
  @ValidateIf((dto: CreateInventoryAdjustmentDto) => dto.locationKind === 'warehouse')
  @IsString()
  @IsNotEmpty()
  locationCode?: string;

  @ApiProperty({ maxLength: 500 })
  @IsString()
  @IsNotEmpty()
  @MaxLength(500)
  reason!: string;

  @ApiProperty({ type: [InventoryAdjustmentLineDto] })
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => InventoryAdjustmentLineDto)
  lines!: InventoryAdjustmentLineDto[];
}
