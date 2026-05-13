import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsIn, IsNotEmpty, IsOptional, IsString, IsNumber, ValidateNested } from 'class-validator';

export class CreateGoodsReceiptSupplierDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  supplierId!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;
}

export class CreateGoodsReceiptLineDto {
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
  orderedQty?: number;

  @ApiProperty({ required: false })
  @IsNumber()
  @IsOptional()
  receivedQty?: number;

  @ApiProperty({ required: false, enum: ['valid', 'invalid', 'damaged'] })
  @IsString()
  @IsOptional()
  @IsIn(['valid', 'invalid', 'damaged'])
  outcome?: string;
}

export class CreateGoodsReceiptDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  poNo?: string;

  @ApiProperty({ required: false, type: CreateGoodsReceiptSupplierDto })
  @ValidateNested()
  @Type(() => CreateGoodsReceiptSupplierDto)
  @IsOptional()
  supplier?: CreateGoodsReceiptSupplierDto;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  invoiceNo?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  invoiceDate?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  remarks?: string;

  @ApiProperty({ required: false, enum: ['draft', 'posted'] })
  @IsString()
  @IsOptional()
  @IsIn(['draft', 'posted'])
  status?: string;

  @ApiProperty({ required: false, type: [CreateGoodsReceiptLineDto] })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => CreateGoodsReceiptLineDto)
  @IsOptional()
  lines?: CreateGoodsReceiptLineDto[];
}

