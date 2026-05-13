import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsNotEmpty, IsNumber, IsOptional, IsString } from 'class-validator';

export class CreateWeightUnitDto {
  @ApiProperty({ example: 'wu-001' })
  @IsString()
  @IsNotEmpty()
  code!: string;

  @ApiProperty({ example: 'Kilogram' })
  @IsString()
  @IsNotEmpty()
  name!: string;

  @ApiProperty({ required: false, example: 'gm' })
  @IsString()
  @IsOptional()
  baseUnit?: string;

  @ApiProperty({ required: false, example: 1000 })
  @IsNumber()
  @IsOptional()
  conversionFactor?: number;

  @ApiProperty({ required: false, default: true })
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;
}
