import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsNotEmpty, IsString } from 'class-validator';

const intentStatuses = ['submitted', 'under_review', 'approved', 'rejected', 'cancelled', 'fulfilled'] as const;

export class SetPurchaseIntentStatusDto {
  @ApiProperty({ enum: intentStatuses })
  @IsString()
  @IsNotEmpty()
  @IsIn(intentStatuses)
  status!: (typeof intentStatuses)[number];
}
