import { ApiProperty } from '@nestjs/swagger';
import { IsString } from 'class-validator';

export class StockTallySaveDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  storeCode!: string;
}
