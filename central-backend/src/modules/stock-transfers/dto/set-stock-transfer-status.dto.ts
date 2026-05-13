import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsNotEmpty, IsString } from 'class-validator';

const statuses = ['draft', 'in_transit', 'awaiting_intake', 'completed', 'cancelled'] as const;

export class SetStockTransferStatusDto {
  @ApiProperty({ enum: statuses })
  @IsString()
  @IsNotEmpty()
  @IsIn(statuses)
  status!: (typeof statuses)[number];
}
