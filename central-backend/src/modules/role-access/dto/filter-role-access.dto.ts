import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsBoolean, IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

const statuses = ['active', 'inactive'] as const;

export class FilterRoleAccessDto {
  @ApiProperty({ required: false, example: 'admin' })
  @IsString()
  @IsOptional()
  role?: string;

  @ApiProperty({ required: false, example: 'core' })
  @IsString()
  @IsOptional()
  area?: string;

  @ApiProperty({ required: false, example: 'Dashboard' })
  @IsString()
  @IsOptional()
  screen?: string;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  allow?: boolean;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 500, minimum: 1, maximum: 500 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  @IsOptional()
  limit?: number;

  @ApiProperty({ required: false, default: 'area' })
  @IsString()
  @IsOptional()
  sortBy?: string;

  @ApiProperty({ required: false, default: 'asc', enum: ['asc', 'desc'] })
  @IsString()
  @IsOptional()
  sortOrder?: 'asc' | 'desc';
}
