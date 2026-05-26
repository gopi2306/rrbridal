import { ApiProperty } from '@nestjs/swagger';
import { Transform } from 'class-transformer';
import { IsInt, IsObject, IsOptional, Max, Min } from 'class-validator';

const undefinedIfNull = ({ value }: { value: unknown }) => (value === null ? undefined : value);

const MAX_LIMIT = 1000;

export class PatchResourceLimitsDto {
  @ApiProperty({
    required: false,
    description: 'User role quotas to update, e.g. { "admin": 10, "store": 8 }',
  })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsObject()
  users?: Record<string, number>;

  @ApiProperty({
    required: false,
    description: 'Maximum number of active stores (enforced on create and reactivation)',
  })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsInt()
  @Min(1)
  @Max(MAX_LIMIT)
  stores?: number;

  @ApiProperty({
    required: false,
    description: 'Maximum active warehouse-type locations (enforced on create and activation)',
  })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsInt()
  @Min(1)
  @Max(MAX_LIMIT)
  warehouses?: number;

  @ApiProperty({
    required: false,
    description:
      'Maximum active/invited users per store (role store, same storeId; enforced on user create/update)',
  })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsInt()
  @Min(1)
  @Max(MAX_LIMIT)
  maxUsersPerStore?: number;

  @ApiProperty({
    required: false,
    description:
      'Maximum active/invited warehouse users per warehouse location code (role warehouse, locationKind warehouse)',
  })
  @Transform(undefinedIfNull)
  @IsOptional()
  @IsInt()
  @Min(1)
  @Max(MAX_LIMIT)
  maxUsersPerWarehouse?: number;
}
