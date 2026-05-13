import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsIn, IsOptional, IsString, ValidateNested } from 'class-validator';
import { CreatePurchaseIntentLineDto } from './create-purchase-intent-line.dto';

const intentStatuses = ['submitted', 'under_review', 'approved', 'rejected', 'cancelled', 'fulfilled'] as const;

export class UpdatePurchaseIntentDto {
  @ApiProperty({ required: false, enum: intentStatuses })
  @IsIn(intentStatuses)
  @IsOptional()
  status?: (typeof intentStatuses)[number];

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  remarks?: string;

  @ApiProperty({ type: [CreatePurchaseIntentLineDto], required: false })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => CreatePurchaseIntentLineDto)
  @IsOptional()
  lines?: CreatePurchaseIntentLineDto[];
}
