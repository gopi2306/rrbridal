import { ApiProperty } from '@nestjs/swagger';
import { IsOptional, IsString, MaxLength } from 'class-validator';

export class ReceiptQrSlotDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(80)
  label?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(2000)
  payload?: string;
}
