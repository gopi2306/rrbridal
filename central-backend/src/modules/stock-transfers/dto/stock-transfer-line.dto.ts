import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsNumber, IsOptional, IsString, Matches, Min } from 'class-validator';

export class StockTransferLineDto {
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
  description?: string;

  @ApiProperty()
  @IsNumber()
  @Min(0.0001)
  qty!: number;
}
