import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsNotEmpty, IsOptional, IsString } from 'class-validator';

export class CreateColourTypeDto {
  @ApiProperty({ example: 'ct-1' })
  @IsString()
  @IsNotEmpty()
  code!: string;

  @ApiProperty({ example: '1 Color' })
  @IsString()
  @IsNotEmpty()
  name!: string;

  @ApiProperty({ required: false, default: true })
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;
}
