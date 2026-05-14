import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsNotEmpty, IsNumber, IsOptional, IsString, ValidateNested } from 'class-validator';

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
  cashDiscPercent?: number;
}

export class CreatePurchaseReturnLineDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  sku!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  description?: string;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  qty?: number;

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

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  purchaseReturnDate?: string;

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
  cashDiscAmount?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  netAmount?: number;

  @ApiProperty({ type: [CreatePurchaseReturnLineDto], required: false })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => CreatePurchaseReturnLineDto)
  @IsOptional()
  lines?: CreatePurchaseReturnLineDto[];
}

