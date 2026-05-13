import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsArray, IsIn, IsNotEmpty, IsOptional, IsString, ValidateNested } from 'class-validator';
import { CreatePurchaseIntentLineDto } from './create-purchase-intent-line.dto';

const intentStatuses = ['submitted', 'under_review', 'approved', 'rejected', 'cancelled', 'fulfilled'] as const;

export class CreatePurchaseIntentDto {
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  storeId!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  deviceId?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  remarks?: string;

  @ApiProperty({ required: false, enum: intentStatuses })
  @IsIn(intentStatuses)
  @IsOptional()
  status?: (typeof intentStatuses)[number];

  @ApiProperty({ type: [CreatePurchaseIntentLineDto] })
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => CreatePurchaseIntentLineDto)
  lines!: CreatePurchaseIntentLineDto[];
}
