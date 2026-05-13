import { ApiProperty } from '@nestjs/swagger';
import { IsInt, IsObject, IsOptional, Min } from 'class-validator';

export class PatchResourceLimitsDto {
  @ApiProperty({ required: false, description: 'User role quotas to update, e.g. { "admin": 10, "store": 8 }' })
  @IsOptional()
  @IsObject()
  users?: Record<string, number>;

  @ApiProperty({ required: false, description: 'Maximum number of active stores' })
  @IsOptional()
  @IsInt()
  @Min(1)
  stores?: number;

  @ApiProperty({ required: false, description: 'Maximum number of warehouses' })
  @IsOptional()
  @IsInt()
  @Min(1)
  warehouses?: number;
}
