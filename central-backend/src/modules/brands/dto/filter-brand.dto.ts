import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { IsBoolean, IsIn, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class FilterBrandDto {
  @ApiProperty({ required: false, description: 'Search across code and name' })
  @IsString()
  @IsOptional()
  search?: string;

  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  code?: string;

  @ApiProperty({ required: false })
  @IsBoolean()
  @IsOptional()
  @Type(() => Boolean)
  isActive?: boolean;

  @ApiProperty({ required: false, default: 1, minimum: 1 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @IsOptional()
  page?: number;

  @ApiProperty({ required: false, default: 20, minimum: 1, maximum: 500 })
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(500)
  @IsOptional()
  limit?: number;

  @ApiProperty({ required: false, default: 'name', enum: ['name', 'code', 'createdAt', 'updatedAt'] })
  @IsString()
  @IsOptional()
  @IsIn(['name', 'code', 'createdAt', 'updatedAt'])
  sortBy?: 'name' | 'code' | 'createdAt' | 'updatedAt';

  @ApiProperty({ required: false, default: 'asc', enum: ['asc', 'desc'] })
  @IsString()
  @IsOptional()
  @IsIn(['asc', 'desc'])
  sortOrder?: 'asc' | 'desc';
}
