import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import {
  IsArray,
  IsIn,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  Matches,
  ValidateNested,
} from 'class-validator';

export class CreatePurchaseReturnSupplierDto {
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

export class CreatePurchaseReturnLineDto {
  @ApiProperty({
    required: false,
    description: 'Product Mongo ObjectId; if omitted, resolved from sku when a product exists',
  })
  @IsString()
  @IsOptional()
  @Matches(/^[a-fA-F0-9]{24}$/i, { message: 'productId must be a 24-character hex Mongo id' })
  productId?: string;

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

export class CreatePurchaseReturnDto {
  @ApiProperty({
    required: false,
    description: 'Business return number (e.g. PR-0001). If omitted, a unique PR-#### number is generated.',
  })
  @IsString()
  @IsOptional()
  purchaseReturnNo?: string;

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

  @ApiProperty({ type: CreatePurchaseReturnSupplierDto })
  @ValidateNested()
  @Type(() => CreatePurchaseReturnSupplierDto)
  supplier!: CreatePurchaseReturnSupplierDto;

  @ApiProperty({ required: false, description: 'Return document date (same role as poDate on purchase orders)' })
  @IsString()
  @IsOptional()
  purchaseReturnDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  deliveryDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  expiryDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  pucOutSlipNo?: string;

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

  @ApiProperty({ required: false, enum: ['open', 'posted', 'closed'] })
  @IsString()
  @IsOptional()
  @IsIn(['open', 'posted', 'closed'])
  status?: string;

  @ApiProperty({ type: [CreatePurchaseReturnLineDto], required: false })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => CreatePurchaseReturnLineDto)
  @IsOptional()
  lines?: CreatePurchaseReturnLineDto[];
}
