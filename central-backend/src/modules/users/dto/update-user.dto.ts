import { ApiProperty } from '@nestjs/swagger';
import { IsIn, IsNotEmpty, IsOptional, IsString, MinLength, ValidateIf } from 'class-validator';

const roles = ['admin', 'warehouse', 'store', 'procurement'] as const;
const locations = ['all', 'warehouse', 'store'] as const;
const statuses = ['active', 'invited', 'disabled'] as const;

export class UpdateUserDto {
  @ApiProperty({ required: false })
  @IsString()
  @IsOptional()
  name?: string;

  @ApiProperty({ required: false })
  @IsString()
  @MinLength(8)
  @IsOptional()
  password?: string;

  @ApiProperty({ required: false, enum: roles })
  @IsIn(roles)
  @IsOptional()
  role?: (typeof roles)[number];

  @ApiProperty({ required: false, enum: locations })
  @IsIn(locations)
  @IsOptional()
  locationKind?: (typeof locations)[number];

  @ApiProperty({ required: false })
  @ValidateIf((o: UpdateUserDto) => o.locationKind === 'store')
  @IsString()
  @IsNotEmpty()
  storeId?: string;

  @ApiProperty({ required: false, enum: statuses })
  @IsIn(statuses)
  @IsOptional()
  status?: (typeof statuses)[number];
}
