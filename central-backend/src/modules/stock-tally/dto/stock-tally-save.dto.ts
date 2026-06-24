import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsOptional, IsString, ValidateNested } from 'class-validator';
import { StockTallyLineInputDto } from './stock-tally-line-input.dto';

export class StockTallySaveDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  storeCode!: string;

  @ApiPropertyOptional({
    type: [StockTallyLineInputDto],
    description:
      'Optional full line list. When provided, replaces the draft session lines and saves them as one tally record.',
  })
  @IsOptional()
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => StockTallyLineInputDto)
  lines?: StockTallyLineInputDto[];
}
