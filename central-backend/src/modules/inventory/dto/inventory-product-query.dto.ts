import { ApiProperty } from '@nestjs/swagger';
import { IsString } from 'class-validator';

export class InventoryProductQueryDto {
  @ApiProperty({
    required: true,
    description: 'Product code. Matches SKU first, then barcode (upcEanCode).',
    example: 'SKU-001',
  })
  @IsString()
  code!: string;
}
