import { ApiProperty } from '@nestjs/swagger';
import { IsOptional, IsString, Matches } from 'class-validator';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export class GstReportQueryDto {
  @ApiProperty({ description: 'From date inclusive (YYYY-MM-DD)', example: '2026-06-01' })
  @IsString()
  @Matches(ISO_DATE, { message: 'from must be YYYY-MM-DD' })
  from!: string;

  @ApiProperty({ description: 'To date inclusive (YYYY-MM-DD)', example: '2026-06-30' })
  @IsString()
  @Matches(ISO_DATE, { message: 'to must be YYYY-MM-DD' })
  to!: string;

  @ApiProperty({ required: false, description: 'Optional store filter for sales only' })
  @IsString()
  @IsOptional()
  storeId?: string;
}
