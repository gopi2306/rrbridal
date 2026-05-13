import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsNotEmpty, IsNumber, IsOptional, IsString, Max, Min } from 'class-validator';

export class CreateHsnCodeDto {
  @ApiProperty({ example: 'hsn-001' })
  @IsString()
  @IsNotEmpty()
  code!: string;

  @ApiProperty({ example: 'HSN 6204' })
  @IsString()
  @IsNotEmpty()
  name!: string;

  @ApiProperty({ example: '6204' })
  @IsString()
  @IsNotEmpty()
  hsnCode!: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  description?: string;

  @ApiProperty({ required: false, example: 18 })
  @IsNumber()
  @Min(0)
  @Max(100)
  @IsOptional()
  gstPercent?: number;

  @ApiProperty({ required: false, default: true })
  @IsBoolean()
  @IsOptional()
  isActive?: boolean;
}
