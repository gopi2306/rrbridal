import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsIn,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  ValidateNested,
} from 'class-validator';

export class CreatePurchaseOrderSupplierDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  supplierId!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  code?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  shortname?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  telPhone?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  mobile?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cashDiscount?: number;
}

export class CreatePurchaseOrderLineDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  sku!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  barcode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  description?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  recdQty?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  freeQty?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cost?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  selling?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  mrp?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  discountPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  discountAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  taxPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  taxAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cgstPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cgstAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  sgstPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  sgstAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  surchargePercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  surchargeAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  amount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  netCost?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  rotPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  grossPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cashDiscPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cashDiscAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  netAmount?: number;
}

export class CreatePurchaseOrderDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  branchId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  mainDivisionId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  mainLocationId?: string;

  @ApiProperty({ type: CreatePurchaseOrderSupplierDto })
  @ValidateNested()
  @Type(() => CreatePurchaseOrderSupplierDto)
  supplier!: CreatePurchaseOrderSupplierDto;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  deliveryDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  expiryDate?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  itemDiscAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cashDiscPercent?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cashDiscount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  taxAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  cgstAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  sgstAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  surchargeAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  netAmount?: number;

  @ApiProperty({ required: false, enum: ['open', 'awaiting_approval', 'approved', 'partially_received', 'received', 'closed'] })
  @IsString()
  @IsOptional()
  @IsIn(['open', 'awaiting_approval', 'approved', 'partially_received', 'received', 'closed'])
  status?: string;

  @ApiProperty({ type: [CreatePurchaseOrderLineDto], required: false })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => CreatePurchaseOrderLineDto)
  @IsOptional()
  lines?: CreatePurchaseOrderLineDto[];
}

