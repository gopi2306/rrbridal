import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsObject, IsOptional, IsString, MaxLength } from 'class-validator';

export class PatchCompanyProfileDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(200)
  legalName?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(200)
  tradeName?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(20)
  gstin?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(500)
  address?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(100)
  city?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(100)
  state?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(20)
  pinCode?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  @MaxLength(40)
  phone?: string;

  @ApiProperty({ required: false })
  @IsEmail()
  @IsOptional()
  @MaxLength(200)
  email?: string;

  @ApiProperty({ required: false, description: 'URL of the company logo image' })
  @IsString()
  @IsOptional()
  @MaxLength(2000)
  companyLogo?: string;

  @ApiProperty({
    required: false,
    description: 'Additional company metadata (replaces the whole map when sent)',
    additionalProperties: true,
  })
  @IsObject()
  @IsOptional()
  extraFields?: Record<string, unknown>;
}
