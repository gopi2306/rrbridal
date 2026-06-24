import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ArrayMinSize, IsArray, IsString, ValidateNested } from 'class-validator';
import { StockTallyLineInputDto } from './stock-tally-line-input.dto';

export class StockTallyReplaceLinesDto {
  @ApiProperty({ example: 'store-001' })
  @IsString()
  storeCode!: string;

  @ApiProperty({
    type: [StockTallyLineInputDto],
    description: 'Full list of scanned lines for this tally session (replaces existing lines)',
  })
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => StockTallyLineInputDto)
  lines!: StockTallyLineInputDto[];
}
